using System.Reflection;
using DRN.Framework.EntityFramework.Context.Interceptors;
using DRN.Framework.EntityFramework.Extensions;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Models;
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
        Validate(serviceProvider, scopedLog, context);

        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        var developmentStatus = serviceProvider.GetRequiredService<DevelopmentStatus>();
        var changeModel = await GetChangeModel(context);

        changeModel.LogChanges(scopedLog, appSettings.Environment.ToString());
        developmentStatus.AddChangeModel(changeModel);

        if (changeModel.Flags is { Migrate: false, HasPendingChanges: false }) return;
        if (changeModel.Flags is { Prototype: true, HasPendingModelChangesForPrototype: true })
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

            if (changeModel.AppliedMigrations.Count == 0)
                await SeedData(context, serviceProvider, appSettings);
            scopedLog?.AddToActions($"{changeModel.Name} migrated {changeModel.PendingMigrations.Count} pending migrations");
        }

        if (changeModel.Flags.HasPendingModelChanges)
            throw new ConfigurationException($"{changeModel.Name} has pending model changes. Create migration or enable Prototype Mode in DrnAppFeatures.");
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

        ValidateEntityTypes(context, scopedLog);
        serviceProvider.GetRequiredService(context.GetType());
    }

    private static async Task SeedData(DbContext context, IServiceProvider serviceProvider, IAppSettings appSettings)
    {
        var optionsAttributes = DbContextConventions.GetContextAttributes(context);
        foreach (var optionsAttribute in optionsAttributes)
            await optionsAttribute.SeedAsync(serviceProvider, appSettings);
    }

    private static async Task<DbContextChangeModel> GetChangeModel(DbContext context)
    {
        var contextName = context.GetType().FullName ?? context.GetType().Name;
        var migrations = context.Database.GetMigrations().ToArray();
        var appliedMigrations = migrations.Length > 0 ? (await context.Database.GetAppliedMigrationsAsync()).ToArray() : [];
        var hasPendingModelChanges = context.Database.HasPendingModelChanges();
        var optionsAttributes = DbContextConventions.GetContextAttributes(context);
        var usePrototypeMode = optionsAttributes.Any(a => a.UsePrototypeMode);
        var usePrototypeModeWhenMigrationExists = optionsAttributes.Any(a => a.UsePrototypeModeWhenMigrationExists);

        var changeModelFlags = new DbContextChangeModelFlags(hasPendingModelChanges, usePrototypeMode, usePrototypeModeWhenMigrationExists);
        var changeModel = new DbContextChangeModel(contextName, migrations, appliedMigrations, changeModelFlags);
        return changeModel;
    }

    private static void ValidateEntityTypes(DbContext context, IScopedLog? scopedLog)
    {
        var entityTypes = context.Model.GetEntityTypes().Select(entityType => entityType.ClrType).ToArray();
        var domainTypes = entityTypes.Where(type => type.IsAssignableTo(typeof(SourceKnownEntity))).ToArray();
        var entityTypePairs = domainTypes.ToDictionary(t => t, t => t.GetCustomAttribute<EntityTypeAttribute>());
        var missingAttributes = entityTypePairs.Where(pair => pair.Value == null).Select(pair => pair.Key.FullName!).ToArray();
        var duplicateAttributePairs = entityTypePairs.Where(pair => pair.Value != null)
            .GroupBy(pair => pair.Value?.EntityType)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .SelectMany(group => group.Select(pair => new DuplicateEntityTypeValue(pair.Key.FullName!, pair.Value!.EntityType))).ToArray();

        var idValidation = new EntityTypeValidationResult(missingAttributes, duplicateAttributePairs);
        if (missingAttributes.Length > 0)
            scopedLog?.Add("EntityTypesMissing", idValidation.MissingEntityTypes);
        if (duplicateAttributePairs.Length > 0)
            scopedLog?.Add("EntityTypesDuplicate", idValidation.DuplicateEntityTypes);

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

            throw new UnprocessableEntityException($"Invalid Entity Type Configuration: {validationDetails}");
        }

        //Validates Entity Type Ids implicitly by calling GetEntityType on Entity
        //This will catch application wide inconsistencies. Previous validation was module-wide;
        var entityTypeValues = domainTypes.Select(SourceKnownEntity.GetEntityType).ToArray();
        _ = entityTypeValues;
    }
}

public record DuplicateEntityTypeValue(string EntityName, ushort EntityType)
{
    public override string ToString() => $"{EntityType}: {EntityName}";
}

public record EntityTypeValidationResult(string[] MissingEntityTypes, DuplicateEntityTypeValue[] DuplicateEntityTypes)
{
    public string GetMissingEntityTypes() => string.Join(',', MissingEntityTypes);
    public string GetDuplicateEntityTypes() => string.Join(',', string.Join(',', DuplicateEntityTypes.Select(p => p.ToString())));
}