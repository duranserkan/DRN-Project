using System.Reflection;
using DRN.Framework.EntityFramework.Context.Interceptors;
using DRN.Framework.EntityFramework.Extensions;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.EntityFramework.Context;

/// <summary>
/// Adds DRNContexts in derived DbContext's assembly by using <br/>
/// <see cref="Extensions.ServiceCollectionExtensions.AddDbContextsWithConventions"/>
/// <br/>
/// when
/// <br/>
/// <see cref="DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes"/>
/// <br/> is called from DbContext's assembly
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DrnContextServiceRegistrationAttribute : ServiceRegistrationAttribute
{
    public override void ServiceRegistration(IServiceCollection sc, Assembly? assembly)
    {
        sc.AddDbContextsWithConventions(assembly);
        //todo replace registrations with attribute registration
        sc.TryAddSingleton<IDrnMaterializationInterceptor, DrnMaterializationInterceptor>();
        sc.TryAddSingleton<IDrnSaveChangesInterceptor, DrnSaveChangesInterceptor>();
        sc.TryAddSingleton<IPaginationUtils, PaginationUtils>();
    }

    public override async Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider, IScopedLog? scopedLog = null)
    {
        if (service is not DbContext context) return;

        for (var i = 0; i < 50; i++)
        {
            //Test CoreEventId.ManyServiceProvidersCreatedWarning which is ignored at DrnContextDefaultsAttribute.ConfigureDbContextOptions
            //If there is an invalid configuration that causes many internal service provider creations, calling this more than 20 times should cause an exception to fail fast.
            using var scopedProvider = serviceProvider.CreateScope();
            scopedProvider.ServiceProvider.GetRequiredService(service.GetType());
        }

        ValidateEntityTypeIds(context, scopedLog);

        serviceProvider.GetRequiredService(service.GetType());

        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        var environment = appSettings.Environment.ToString();
        var contextName = context.GetType().FullName!;

        var migrations = context.Database.GetMigrations().ToArray();
        var appliedMigrations = migrations.Length > 0 ? (await context.Database.GetAppliedMigrationsAsync()).ToArray() : [];
        var hasPendingModelChanges = context.Database.HasPendingModelChanges();
        var optionsAttributes = DbContextConventions.GetContextAttributes(context);
        var usePrototypeModeWhenMigrationExists = optionsAttributes.Any(a => a.UsePrototypeModeWhenMigrationExists);
        var changeModel = new DbContextChangeModel(migrations, appliedMigrations, hasPendingModelChanges, usePrototypeModeWhenMigrationExists);

        scopedLog?.AddToActions($"{contextName} has {migrations.Length} migrations");
        scopedLog?.AddToActions($"{contextName} has {appliedMigrations.Length} applied migrations. Last applied: {changeModel.LastAppliedMigration}");
        scopedLog?.AddToActions($"{contextName} has {changeModel.PendingMigrations.Length} pending migrations. Last pending: {changeModel.LastPendingMigration}");
        scopedLog?.AddToActions($"{contextName} has {migrations.Length} total migrations");
        scopedLog?.AddToActions($"{contextName} has pending model changes");

        var migrate = appSettings is { IsDevEnvironment: true, DevelopmentSettings.AutoMigrate: true };
        if (!migrate)
        {
            scopedLog?.AddToActions($"{contextName} auto migration disabled in {environment}");
            return;
        }

        if (!changeModel.HasPendingChanges)
        {
            if (appSettings.DevelopmentSettings.PrototypingMode)
                scopedLog?.AddToActions($"existing {contextName} db is used for prototyping mode since there is no pending changes");
            return;
        }

        if (appSettings.DevelopmentSettings.PrototypingMode && changeModel.HasPendingModelChangesForPrototypingMode)
        {
            scopedLog?.AddToActions($"checking {contextName} database in prototyping mode.");

            var created = await context.Database.EnsureCreatedAsync();
            if (!created)
            {
                scopedLog?.AddToActions($"{contextName} db will be recreated for pending model changes.");
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
                await SeedData(context, serviceProvider, appSettings);
                scopedLog?.AddToActions($"{contextName} db recreated for pending model changes.");

                return;
            }

            await SeedData(context, serviceProvider, appSettings);

            scopedLog?.AddToActions($"{contextName} db created for prototyping mode");

            return;
        }

        if (changeModel.HasPendingMigrationsWithoutPendingModelChanges)
        {
            scopedLog?.AddToActions($"{contextName} is migrating {environment}");
            await context.Database.MigrateAsync();

            if (appliedMigrations.Length == 0)
                await SeedData(context, serviceProvider, appSettings);
            scopedLog?.AddToActions($"{contextName} migrated {changeModel.PendingMigrations.Length} pending migrations");
        }

        if (hasPendingModelChanges)
            throw new ConfigurationException($"{contextName} has pending model changes. Create migration or enable PrototypingMode in DrnAppFeatures.");
    }

    private static async Task SeedData(DbContext context, IServiceProvider serviceProvider, IAppSettings appSettings)
    {
        var optionsAttributes = DbContextConventions.GetContextAttributes(context);
        foreach (var optionsAttribute in optionsAttributes)
            await optionsAttribute.SeedAsync(serviceProvider, appSettings);
    }

    private static void ValidateEntityTypeIds(DbContext context, IScopedLog? scopedLog)
    {
        var entityTypes = context.Model.GetEntityTypes().Select(entityType => entityType.ClrType).ToArray();
        var domainTypes = entityTypes.Where(type => type.IsAssignableTo(typeof(SourceKnownEntity))).ToArray();
        var entityTypeIdPairs = domainTypes.ToDictionary(t => t, t => t.GetCustomAttribute<EntityTypeIdAttribute>());
        var missingAttributes = entityTypeIdPairs.Where(pair => pair.Value == null).Select(pair => pair.Key.FullName!).ToArray();
        var duplicateAttributePairs = entityTypeIdPairs.Where(pair => pair.Value != null)
            .GroupBy(pair => pair.Value?.Id)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .SelectMany(group => group.Select(pair => new DuplicateEntityTypeIdValue(pair.Key.FullName!, pair.Value!.Id))).ToArray();

        var idValidation = new EntityTypeIdValidationResult(missingAttributes, duplicateAttributePairs);
        if (missingAttributes.Length > 0)
            scopedLog?.Add("EntityTypeMissingIds", idValidation.MissingEntityTypeIds);
        if (duplicateAttributePairs.Length > 0)
            scopedLog?.Add("EntityTypeDuplicateIds", idValidation.DuplicateEntityTypeIds);

        if (missingAttributes.Length > 0 || duplicateAttributePairs.Length > 0)
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
            }

            throw new UnprocessableEntityException($"Invalid Entity Type Id Configuration: {validationDetails}");
        }

        //Validates Entity Type Ids implicitly by calling GetEntityTypeId on Entity
        //This will catch application wide inconsistencies. Previous validation was module-wide;
        var entityTypeIds = domainTypes.Select(SourceKnownEntity.GetEntityTypeId).ToArray();
        _ = entityTypeIds;
    }
}

public record DuplicateEntityTypeIdValue(string EntityName, ushort EntityTypeId)
{
    public override string ToString() => $"{EntityTypeId}: {EntityName}";
}

public record EntityTypeIdValidationResult(string[] MissingEntityTypeIds, DuplicateEntityTypeIdValue[] DuplicateEntityTypeIds)
{
    public string GetEntityTypeMissingIds() => string.Join(',', MissingEntityTypeIds);
    public string GetEntityTypeDuplicateIds() => string.Join(',', string.Join(',', DuplicateEntityTypeIds.Select(p => p.ToString())));
}

public class DbContextChangeModel
{
    public DbContextChangeModel(string[] migrations, string[] appliedMigrations, bool hasPendingModelChanges, bool usePrototypeModeWhenMigrationExists)
    {
        Migrations = migrations;
        AppliedMigrations = appliedMigrations;
        PendingMigrations = migrations.Except(appliedMigrations).ToArray();
        LastAppliedMigration = appliedMigrations.LastOrDefault() ?? "n/a";
        LastPendingMigration = PendingMigrations.LastOrDefault() ?? "n/a";
        HasPendingMigrations = PendingMigrations.Length > 0;
        HasPendingModelChanges = hasPendingModelChanges;

        HasPendingChanges = HasPendingMigrations || HasPendingModelChanges;
        UsePrototypeModeWhenMigrationExists = usePrototypeModeWhenMigrationExists;
        HasPendingModelChangesForPrototypingMode = (migrations.Length == 0 && hasPendingModelChanges) ||
                                                   (migrations.Length > 0 && usePrototypeModeWhenMigrationExists && hasPendingModelChanges);
    }

    public string[] Migrations { get; }
    public string[] AppliedMigrations { get; }
    public string[] PendingMigrations { get; }
    public string LastAppliedMigration { get; }
    public string LastPendingMigration { get; }
    public bool HasPendingMigrations { get; }
    public bool HasPendingModelChanges { get; }
    public bool HasPendingChanges { get; }
    public bool HasPendingModelChangesForPrototypingMode { get; }
    public bool UsePrototypeModeWhenMigrationExists { get; }
    public bool HasPendingMigrationsWithoutPendingModelChanges => PendingMigrations.Length > 0 && !HasPendingModelChanges;
}