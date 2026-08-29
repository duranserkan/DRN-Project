using System.Reflection;
using DRN.Framework.EntityFramework.Attributes;
using DRN.Framework.EntityFramework.Context.Interceptors;
using DRN.Framework.EntityFramework.Extensions;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Models;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace DRN.Framework.EntityFramework.Context;

/// <summary>
/// Adds DRNContexts in derived DbContext's assembly by using <br/>
/// <see cref="Extensions.ServiceCollectionExtensions.AddDbContextsWithConventions"/>
/// <br/>
/// when
/// <br/>
/// <see cref="DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(IServiceCollection)"/>
/// <br/> is called from DbContext's assembly
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DrnContextServiceRegistrationAttribute : ServiceRegistrationAttribute
{
    public override void ServiceRegistration(IServiceCollection sc, Assembly? assembly)
    {
        sc.AddDbContextsWithConventions(assembly);
        sc.TryAddSingleton<IDrnMaterializationInterceptor, DrnMaterializationInterceptor>();
        sc.TryAddSingleton<IDrnSaveChangesInterceptor, DrnSaveChangesInterceptor>();
        sc.TryAddSingleton<IPaginationUtils, PaginationUtils>();
    }

    public override async Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider, IScopedLog? scopedLog = null)
    {
        if (service is not DbContext context) return;
        Validate(serviceProvider, scopedLog, context);

        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        var changeModel = await GetChangeModel(serviceProvider, context);
        changeModel.LogChanges(scopedLog, appSettings.Environment.ToString());

        if (changeModel.Flags is { Migrate: false})
            return;

        if (changeModel.Flags.RecreatePrototypeDatabaseForPendingModelChanges)
        {
            scopedLog?.AddToActions($"checking {changeModel.Name} database in prototype mode.");
            var created = await context.Database.EnsureCreatedAsync();
            if (created)
                scopedLog?.AddToActions($"{changeModel.Name} db created for prototype mode");
            else
            {
                scopedLog?.AddToActions($"{changeModel.Name} db will be recreated for pending model changes.");
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
                scopedLog?.AddToActions($"{changeModel.Name} db recreated for pending model changes.");
            }

            await SeedData(context, serviceProvider, appSettings);

            return;
        }

        if (changeModel.Flags.HasPendingMigrationsWithoutPendingModelChanges)
        {
            scopedLog?.AddToActions($"{changeModel.Name} is migrating {appSettings.Environment.ToString()}");
            await context.Database.MigrateAsync();

            await SeedData(context, serviceProvider, appSettings);
            scopedLog?.AddToActions($"{changeModel.Name} migrated {changeModel.PendingMigrations.Count} pending migrations");
        }

        if (changeModel.Flags.HasPendingModelChanges)
        {
            if (changeModel.Flags.DevelopmentSettingsPrototypeFlag && changeModel.AppliedMigrations.Count > 0 && !changeModel.Flags.UsePrototypeModeWhenMigrationExists)
                throw new ConfigurationException($"{changeModel.Name} has pending model changes, but prototype recreation is blocked because migrations are applied to the database. Create migration or enable UsePrototypeModeWhenMigrationExists.");

            throw new ConfigurationException($"{changeModel.Name} has pending model changes. Create migration or enable Prototype Mode in DrnDevelopmentSettings.");
        }
    }

    private static void Validate(IServiceProvider serviceProvider, IScopedLog? scopedLog, DbContext context)
    {
        for (var i = 0; i < 50; i++)
        {
            //Test CoreEventId.ManyServiceProvidersCreatedWarning which is ignored at DrnContextDefaultsAttribute.ConfigureDbContextOptions
            //If there is an invalid configuration that causes many internal service provider creations, calling this more than 20 times should cause an exception to fail fast.
            using var scopedProvider = serviceProvider.CreateScope();
            scopedProvider.ServiceProvider.GetRequiredService(context.GetType());
        }

        var appSettings = serviceProvider.GetService<IAppSettings>();
        PreValidateAllDbContexts(serviceProvider, scopedLog, appSettings);
        ValidateEntityTypes(context, scopedLog, appSettings, serviceProvider);
        serviceProvider.GetRequiredService(context.GetType());
    }

    private static void PreValidateAllDbContexts(IServiceProvider serviceProvider, IScopedLog? scopedLog, IAppSettings? appSettings)
    {
        var allDbContexts = new List<DbContext>();
        var containers = serviceProvider.GetServices<DrnServiceContainer>();
        foreach (var container in containers)
        {
            foreach (var module in container.AttributeSpecifiedModules)
            {
                if (module.ModuleAttribute is not DrnContextServiceRegistrationAttribute) continue;
                foreach (var descriptor in module.ServiceDescriptors)
                {
                    if (descriptor.ServiceType.IsAssignableTo(typeof(DbContext)))
                    {
                        if (serviceProvider.GetRequiredService(descriptor.ServiceType) is DbContext dbContext)
                            allDbContexts.Add(dbContext);
                    }
                }
            }
        }

        var allDomainTypes = allDbContexts.SelectMany(GetAllDomainEntityTypes).Distinct().ToArray();
        EntityTypeRegistry.Register(allDomainTypes);
        SourceKnownIdUtils.Warmup(allDomainTypes);

        foreach (var dbContext in allDbContexts)
            ValidateEntityTypes(dbContext, scopedLog, appSettings, serviceProvider);
    }

    /// <summary>
    /// Invokes <see cref="NpgsqlDbContextOptionsAttribute.SeedAsync"/> on registered context option attributes.
    /// See <see href="https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding"/> for EF Core data seeding guidance.
    /// </summary>
    private static async Task SeedData(DbContext context, IServiceProvider serviceProvider, IAppSettings appSettings)
    {
        var optionsAttributes = DbContextConventions.GetContextAttributes(context);
        foreach (var optionsAttribute in optionsAttributes)
            await optionsAttribute.SeedAsync(serviceProvider, appSettings);
    }

    private static async Task<DbContextChangeModel> GetChangeModel(IServiceProvider serviceProvider, DbContext context)
    {
        var contextName = context.GetType().FullName ?? context.GetType().Name;
        var migrations = context.Database.GetMigrations().ToArray();
        // Always query target database migration history directly rather than conditioning on assembly migrations (e.g., migrations.Length > 0).
        // If we only query the DB when the assembly contains migrations, an assembly with missing/deleted migration files
        // would record 0 applied migrations, causing pending-model detection to treat the database as unmigrated and execute
        // EnsureDeletedAsync(), wiping a populated database.
        // If the target database does not exist yet (e.g., initial prototype startup), treat as 0 applied migrations so EnsureCreatedAsync can run.
        string[] appliedMigrations;
        try
        {
            appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            appliedMigrations = [];
        }
        var hasPendingModelChanges = context.Database.HasPendingModelChanges();
        var optionsAttributes = DbContextConventions.GetContextAttributes(context);
        var contextOptionsUsePrototypeModeFlag = optionsAttributes.Any(a => a.UsePrototypeMode);
        var usePrototypeModeWhenMigrationExists = optionsAttributes.Any(a => a.UsePrototypeModeWhenMigrationExists);

        var changeModelFlags = new DbContextChangeModelFlags(hasPendingModelChanges, contextOptionsUsePrototypeModeFlag, usePrototypeModeWhenMigrationExists);
        var changeModel = new DbContextChangeModel(contextName, migrations, appliedMigrations, changeModelFlags);
        var developmentStatus = serviceProvider.GetRequiredService<DevelopmentStatus>();
        developmentStatus.AddChangeModel(changeModel);

        return changeModel;
    }

    internal static void ValidateEntityTypes(DbContext context, IScopedLog? scopedLog, IAppSettings? appSettings = null, IServiceProvider? serviceProvider = null)
    {
        var domainTypes = GetAllDomainEntityTypes(context);
        var idValidation = GetEntityTypeValidationResult(domainTypes);
        var missingAttributes = idValidation.MissingEntityTypes;
        var duplicateAttributePairs = idValidation.DuplicateEntityTypes;
        var multipleAppIds = idValidation.MultipleAppIds;

        if (missingAttributes.Length > 0)
            scopedLog?.Add("EntityTypesMissing", idValidation.MissingEntityTypes);
        if (duplicateAttributePairs.Length > 0)
            scopedLog?.Add("EntityTypesDuplicate", idValidation.DuplicateEntityTypes);
        if (multipleAppIds.Length > 0)
            scopedLog?.Add("EntityTypesMultipleAppIds", idValidation.MultipleAppIds);

        if (missingAttributes.Length > 0 || duplicateAttributePairs.Length > 0 || multipleAppIds.Length > 0)
        {
            var validationDetails = string.Empty;
            if (scopedLog == null)
                validationDetails = idValidation.Serialize();
            else
            {
                if (missingAttributes.Length > 0)
                    validationDetails += " Check: EntityTypeMissingIds.";
                if (duplicateAttributePairs.Length > 0)
                    validationDetails += " Check: EntityTypeDuplicateIds.";
                if (multipleAppIds.Length > 0)
                    validationDetails += $" Check: MultipleAppIds ({string.Join(", ", multipleAppIds)}).";
            }

            throw new UnprocessableEntityException($"Invalid Entity Type Configuration: {validationDetails}");
        }

        // Validates and bulk registers domain entity types into the immutable EntityTypeRegistry.
        // This catches application-wide inconsistencies and freezes the lookup snapshot.
        EntityTypeRegistry.Register(domainTypes);

        var configuredAppId = appSettings?.NexusAppSettings.AppId ?? 0;
        if (idValidation.NonTestAppIds.Length == 1)
        {
            var domainAppId = idValidation.NonTestAppIds[0];
            var hostAppIds = GetHostDomainAppIds(serviceProvider, context);
            var isMatched = configuredAppId == domainAppId || hostAppIds.Contains(configuredAppId);
            if (!isMatched)
                throw new ConfigurationException($"NexusAppSettings:AppId ({configuredAppId}) does not match {context.GetType().Name} domain partition AppId ({domainAppId}) or any registered domain partition in the host.");
        }
    }

    private static byte[] GetHostDomainAppIds(IServiceProvider? serviceProvider, DbContext context)
    {
        if (serviceProvider == null)
            return [];

        var hostAppIds = new HashSet<byte>();
        var containers = serviceProvider.GetServices<DrnServiceContainer>();
        foreach (var container in containers)
        {
            foreach (var module in container.AttributeSpecifiedModules)
            {
                if (module.ModuleAttribute is not DrnContextServiceRegistrationAttribute) continue;
                foreach (var descriptor in module.ServiceDescriptors)
                {
                    if (descriptor.ServiceType.IsAssignableTo(typeof(DbContext)))
                    {
                        if (serviceProvider.GetService(descriptor.ServiceType) is DbContext dbContext)
                        {
                            var domainTypes = GetModelDomainEntityTypes(dbContext);
                            foreach (var type in domainTypes)
                            {
                                var attr = type.GetCustomAttribute<EntityTypeAttribute>();
                                if (attr != null && attr.AppId != IAppId.TestAppId)
                                    hostAppIds.Add(attr.AppId);
                            }
                        }
                    }
                }
            }
        }

        foreach (var type in GetModelDomainEntityTypes(context))
        {
            var attr = type.GetCustomAttribute<EntityTypeAttribute>();
            if (attr != null && attr.AppId != IAppId.TestAppId)
                hostAppIds.Add(attr.AppId);
        }

        return hostAppIds.ToArray();
    }

    internal static Type[] GetModelDomainEntityTypes(DbContext context) =>
        context.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => t.IsAssignableTo(typeof(SourceKnownEntity)))
            .Distinct()
            .ToArray();

    internal static EntityTypeValidationResult GetEntityTypeValidationResult(IReadOnlyCollection<Type> domainTypes)
    {
        var entityTypePairs = domainTypes.ToDictionary(t => t, t => t.GetCustomAttribute<EntityTypeAttribute>());
        var missingAttributes = entityTypePairs.Where(pair => pair.Value == null).Select(pair => pair.Key.FullName!).ToArray();
        var duplicateAttributePairs = entityTypePairs.Where(pair => pair.Value != null)
            .GroupBy(pair => new EntityTypeId(pair.Value!.EntityType, pair.Value.AppId))
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .SelectMany(group => group.Select(pair => new DuplicateEntityTypeValue(pair.Key.FullName!, pair.Value!.EntityType))).ToArray();

        var nonTestAppIds = entityTypePairs.Values
            .Where(attr => attr != null && attr.AppId != IAppId.TestAppId)
            .Select(attr => attr!.AppId)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        var multipleAppIds = nonTestAppIds.Length > 1 ? nonTestAppIds : [];

        return new EntityTypeValidationResult(missingAttributes, duplicateAttributePairs, multipleAppIds, nonTestAppIds);
    }

    internal static Type[] GetAllDomainEntityTypes(DbContext context)
    {
        var modelTypes = context.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => t.IsAssignableTo(typeof(SourceKnownEntity)));

        var contextAssembly = context.GetType().Assembly;
        var assemblies = IsTestAssembly(contextAssembly) ? [] : new[] { contextAssembly };

        var assemblyTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                        t.IsAssignableTo(typeof(SourceKnownEntity)));

        return modelTypes.Concat(assemblyTypes).Distinct().ToArray();
    }

    private static bool IsTestAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? string.Empty;
        return name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
               name.Contains(".Test.", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
               name.Contains(".Tests.", StringComparison.OrdinalIgnoreCase);
    }
}

public record DuplicateEntityTypeValue(string EntityName, ushort EntityType)
{
    public override string ToString() => $"{EntityType}: {EntityName}";
}

public record EntityTypeValidationResult(
    string[] MissingEntityTypes,
    DuplicateEntityTypeValue[] DuplicateEntityTypes,
    byte[]? MultipleAppIds = null,
    byte[]? NonTestAppIds = null)
{
    public byte[] MultipleAppIds { get; init; } = MultipleAppIds ?? [];
    public byte[] NonTestAppIds { get; init; } = NonTestAppIds ?? [];
    public string GetMissingEntityTypes() => string.Join(',', MissingEntityTypes);
    public string GetDuplicateEntityTypes() => string.Join(',', DuplicateEntityTypes.Select(p => p.ToString()));
    public string GetMultipleAppIds() => string.Join(',', MultipleAppIds);
}
