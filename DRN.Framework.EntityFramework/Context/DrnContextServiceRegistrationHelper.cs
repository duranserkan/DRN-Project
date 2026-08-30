using System.Reflection;
using System.Text;
using DRN.Framework.EntityFramework.Attributes;
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
using Npgsql;

namespace DRN.Framework.EntityFramework.Context;

internal static class DrnContextServiceRegistrationHelper
{
    internal static void WarmupScopedProviders(IServiceProvider serviceProvider, DbContext context)
    {
        for (var i = 0; i < 50; i++)
        {
            // Test CoreEventId.ManyServiceProvidersCreatedWarning which is ignored at DrnContextDefaultsAttribute.ConfigureDbContextOptions
            // If there is an invalid configuration that causes many internal service provider creations, calling this more than 20 times should cause an exception to fail fast.
            using var scopedProvider = serviceProvider.CreateScope();
            scopedProvider.ServiceProvider.GetRequiredService(context.GetType());
        }
    }

    internal static async Task<DbContextChangeModel> GetChangeModelAsync(IServiceProvider serviceProvider, DbContext context)
    {
        var contextName = context.GetType().FullName ?? context.GetType().Name;
        var migrations = context.Database.GetMigrations().ToArray();
        // Always query target database migration history directly rather than conditioning on assembly migrations (e.g., migrations.Length > 0).
        // If we only query the DB when the assembly contains migrations, an assembly with missing/deleted migration files
        // would record 0 applied migrations, causing pending-model detection to treat the database as unmigrated and execute
        // EnsureDeletedAsync(), wiping a populated database.
        // If the target database does not exist yet (e.g., initial prototype startup), treat as 0 applied migrations so EnsureCreatedAsync can run.
        var appliedMigrations = await GetAppliedMigrationsSafeAsync(context);
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

    private static async Task<string[]> GetAppliedMigrationsSafeAsync(DbContext context)
    {
        try
        {
            return (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            return [];
        }
    }

    internal static async Task SeedDataAsync(DbContext context, IServiceProvider serviceProvider, IAppSettings appSettings)
    {
        var optionsAttributes = DbContextConventions.GetContextAttributes(context);
        foreach (var optionsAttribute in optionsAttributes)
            await optionsAttribute.SeedAsync(serviceProvider, appSettings);
    }

    internal static async Task ProcessChangeModelAsync(
        DbContext context,
        IServiceProvider serviceProvider,
        IAppSettings appSettings,
        DbContextChangeModel changeModel,
        IScopedLog? scopedLog)
    {
        if (changeModel.Flags.Migrate)
        {
            if (changeModel.Flags.RecreatePrototypeDatabaseForPendingModelChanges)
            {
                await RecreatePrototypeDatabaseAsync(context, serviceProvider, appSettings, changeModel, scopedLog);
                return;
            }

            if (changeModel.Flags.HasPendingMigrationsWithoutPendingModelChanges)
            {
                await ApplyPendingMigrationsAsync(context, serviceProvider, appSettings, changeModel, scopedLog);
            }
        }

        VerifyPendingModelChanges(changeModel);
    }

    internal static async Task RecreatePrototypeDatabaseAsync(
        DbContext context,
        IServiceProvider serviceProvider,
        IAppSettings appSettings,
        DbContextChangeModel changeModel,
        IScopedLog? scopedLog)
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

        await SeedDataAsync(context, serviceProvider, appSettings);
    }

    internal static async Task ApplyPendingMigrationsAsync(
        DbContext context,
        IServiceProvider serviceProvider,
        IAppSettings appSettings,
        DbContextChangeModel changeModel,
        IScopedLog? scopedLog)
    {
        scopedLog?.AddToActions($"{changeModel.Name} is migrating {appSettings.Environment.ToString()}");
        await context.Database.MigrateAsync();

        await SeedDataAsync(context, serviceProvider, appSettings);
        scopedLog?.AddToActions($"{changeModel.Name} migrated {changeModel.PendingMigrations.Count} pending migrations");
    }

    internal static void VerifyPendingModelChanges(DbContextChangeModel changeModel)
    {
        if (!changeModel.Flags.HasPendingModelChanges)
            return;

        if (changeModel.Flags.DevelopmentSettingsPrototypeFlag &&
            changeModel.AppliedMigrations.Count > 0 &&
            !changeModel.Flags.UsePrototypeModeWhenMigrationExists)
        {
            throw new ConfigurationException(
                $"{changeModel.Name} has pending model changes, but prototype recreation is blocked because migrations are applied to the database. Create migration or enable UsePrototypeModeWhenMigrationExists.");
        }

        throw new ConfigurationException(
            $"{changeModel.Name} has pending model changes. Create migration or enable Prototype Mode in DrnDevelopmentSettings.");
    }

    internal static List<DbContext> CollectAllDbContexts(IServiceProvider serviceProvider, DbContext? currentContext = null)
    {
        var allDbContexts = GetRegisteredDbContexts(serviceProvider);
        if (currentContext != null && !allDbContexts.Any(c => c.GetType() == currentContext.GetType()))
            allDbContexts.Add(currentContext);

        return allDbContexts;
    }

    internal static List<DbContext> GetRegisteredDbContexts(IServiceProvider serviceProvider) =>
        serviceProvider.GetServices<DrnServiceContainer>()
            .SelectMany(container => container.AttributeSpecifiedModules)
            .Where(module => module.ModuleAttribute is DrnContextServiceRegistrationAttribute)
            .SelectMany(module => module.ServiceDescriptors)
            .Where(descriptor => descriptor.ServiceType.IsAssignableTo(typeof(DbContext)))
            .Select(descriptor => serviceProvider.GetService(descriptor.ServiceType))
            .OfType<DbContext>()
            .ToList();

    internal static void ValidateHostEntityTypes(EntityTypeValidationResult hostValidation, IScopedLog? scopedLog)
    {
        var missingAttributes = hostValidation.MissingEntityTypes;
        var duplicateAttributePairs = hostValidation.DuplicateEntityTypes;

        if (missingAttributes.Length > 0)
            scopedLog?.Add("HostEntityTypesMissing", missingAttributes);
        if (duplicateAttributePairs.Length > 0)
            scopedLog?.Add("HostEntityTypesDuplicate", duplicateAttributePairs);

        if (missingAttributes.Length == 0 && duplicateAttributePairs.Length == 0)
            return;

        var validationDetails = BuildValidationDetails(hostValidation, scopedLog, includeMultipleAppIds: false);
        throw new UnprocessableEntityException($"Invalid Host Entity Type Configuration: {validationDetails}");
    }

    internal static void ValidateModelEntityTypes(EntityTypeValidationResult idValidation, IScopedLog? scopedLog)
    {
        var missingAttributes = idValidation.MissingEntityTypes;
        var duplicateAttributePairs = idValidation.DuplicateEntityTypes;
        var multipleAppIds = idValidation.MultipleAppIds;

        if (missingAttributes.Length > 0)
            scopedLog?.Add("EntityTypesMissing", missingAttributes);
        if (duplicateAttributePairs.Length > 0)
            scopedLog?.Add("EntityTypesDuplicate", duplicateAttributePairs);
        if (multipleAppIds.Length > 0)
            scopedLog?.Add("EntityTypesMultipleAppIds", multipleAppIds);

        if (missingAttributes.Length == 0 && duplicateAttributePairs.Length == 0 && multipleAppIds.Length == 0)
            return;

        var validationDetails = BuildValidationDetails(idValidation, scopedLog, includeMultipleAppIds: true);
        throw new UnprocessableEntityException($"Invalid Entity Type Configuration: {validationDetails}");
    }

    private static string BuildValidationDetails(
        EntityTypeValidationResult validation,
        IScopedLog? scopedLog,
        bool includeMultipleAppIds)
    {
        if (scopedLog == null)
            return validation.Serialize();

        var details = new StringBuilder();
        if (validation.MissingEntityTypes.Length > 0)
            details.Append(" Check: EntityTypeMissingIds.");
        if (validation.DuplicateEntityTypes.Length > 0)
            details.Append(" Check: EntityTypeDuplicateIds.");
        if (includeMultipleAppIds && validation.MultipleAppIds.Length > 0)
            details.Append($" Check: MultipleAppIds ({string.Join(", ", validation.MultipleAppIds)}).");

        return details.ToString();
    }

    internal static void ValidateAppIdPartition(
        DbContext context,
        EntityTypeValidationResult idValidation,
        IAppSettings? appSettings,
        IServiceProvider? serviceProvider)
    {
        if (idValidation.NonTestAppIds.Length != 1)
            return;

        var domainAppId = idValidation.NonTestAppIds[0];
        var configuredAppId = appSettings?.NexusAppSettings.AppId ?? 0;
        var hostAppIds = GetHostDomainAppIds(serviceProvider, context);
        var isMatched = configuredAppId == domainAppId || hostAppIds.Contains(configuredAppId);

        if (!isMatched)
        {
            throw new ConfigurationException(
                $"NexusAppSettings:AppId ({configuredAppId}) does not match {context.GetType().Name} domain partition AppId ({domainAppId}) or any registered domain partition in the host.");
        }
    }

    internal static byte[] GetHostDomainAppIds(IServiceProvider? serviceProvider, DbContext context)
    {
        if (serviceProvider == null)
            return [];

        var hostAppIds = new HashSet<byte>();
        var registeredContexts = GetRegisteredDbContexts(serviceProvider);
        foreach (var dbContext in registeredContexts)
            CollectNonTestAppIds(dbContext, hostAppIds);

        CollectNonTestAppIds(context, hostAppIds);
        return hostAppIds.ToArray();
    }

    private static void CollectNonTestAppIds(DbContext context, ISet<byte> appIds)
    {
        var nonTestAppIds = GetModelDomainEntityTypes(context)
            .Select(type => type.GetCustomAttribute<EntityTypeAttribute>())
            .Where(attr => attr != null && attr.AppId != IAppId.TestAppId)
            .Select(attr => attr!.AppId);

        appIds.UnionWith(nonTestAppIds);
    }

    internal static Type[] GetModelDomainEntityTypes(DbContext context) =>
        context.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => t.IsAssignableTo(typeof(SourceKnownEntity)))
            .Distinct()
            .ToArray();

    internal static Type[] GetHostDomainEntityTypes(IServiceProvider? serviceProvider, IReadOnlyCollection<DbContext>? dbContexts = null)
    {
        var contexts = dbContexts ?? [];
        var modelTypes = contexts.SelectMany(GetModelDomainEntityTypes).Distinct().ToArray();
        var assemblies = CollectNonTestAssemblies(serviceProvider, contexts, modelTypes);

        var assemblyTypes = assemblies
            .SelectMany(GetAssemblyDomainEntityTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, IsNestedPrivate: false } &&
                        t.IsAssignableTo(typeof(SourceKnownEntity)));

        return modelTypes.Concat(assemblyTypes).Distinct().ToArray();
    }

    private static HashSet<Assembly> CollectNonTestAssemblies(
        IServiceProvider? serviceProvider,
        IReadOnlyCollection<DbContext> contexts,
        Type[] modelTypes)
    {
        var containerAssemblies = serviceProvider?.GetServices<DrnServiceContainer>().Select(container => container.Assembly) ?? [];
        var contextAssemblies = contexts.Select(context => context.GetType().Assembly);
        var modelAssemblies = modelTypes.Select(modelType => modelType.Assembly);

        return containerAssemblies
            .Concat(contextAssemblies)
            .Concat(modelAssemblies)
            .Where(assembly => !IsTestAssembly(assembly))
            .ToHashSet();
    }

    internal static Type[] GetAssemblyDomainEntityTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var loaderMessages = ex.LoaderExceptions
                .Where(e => e != null)
                .Select(e => e!.Message)
                .Distinct()
                .ToArray();

            var diagnostics = loaderMessages.Length > 0
                ? string.Join("; ", loaderMessages)
                : ex.Message;

            var assemblyName = assembly.GetName().Name ?? assembly.FullName ?? "Unknown";

            throw new InvalidOperationException(
                $"Failed to load types from assembly '{assemblyName}' for domain entity validation. Loader exceptions: {diagnostics}",
                ex);
        }
    }

    internal static EntityTypeValidationResult GetEntityTypeValidationResult(IReadOnlyCollection<Type> domainTypes)
    {
        var entityTypePairs = domainTypes.ToDictionary(t => t, t => t.GetCustomAttribute<EntityTypeAttribute>());
        var missingAttributes = entityTypePairs.Where(pair => pair.Value == null).Select(pair => pair.Key.FullName!).ToArray();
        var duplicateAttributePairs = entityTypePairs.Where(pair => pair.Value != null)
            .GroupBy(pair => new EntityTypeId(pair.Value!.EntityType, pair.Value.AppId))
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .SelectMany(group => group.Select(pair => new DuplicateEntityTypeValue(pair.Key.FullName!, pair.Value!.EntityType, pair.Value.AppId))).ToArray();

        var nonTestAppIds = entityTypePairs.Values
            .Where(attr => attr != null && attr.AppId != IAppId.TestAppId)
            .Select(attr => attr!.AppId)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        var multipleAppIds = nonTestAppIds.Length > 1 ? nonTestAppIds : [];

        return new EntityTypeValidationResult(missingAttributes, duplicateAttributePairs, multipleAppIds, nonTestAppIds);
    }

    internal static Type[] GetAllDomainEntityTypes(DbContext context) => GetModelDomainEntityTypes(context);

    private static bool IsTestAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? string.Empty;
        return name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
               name.Contains(".Test.", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
               name.Contains(".Tests.", StringComparison.OrdinalIgnoreCase);
    }
}
