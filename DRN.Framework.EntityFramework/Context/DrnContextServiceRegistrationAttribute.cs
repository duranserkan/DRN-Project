using System.Reflection;
using DRN.Framework.EntityFramework.Extensions;
using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.EntityFramework.Context;

/// <summary>
/// Adds DRNContexts in derived DbContext's assembly by using <br/>
/// <see cref="ServiceCollectionExtensions.AddDbContextsWithConventions"/>
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
        => sc.AddDbContextsWithConventions(assembly);

    public override async Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider, IScopedLog? scopedLog = null)
    {
        if (service is not DbContext context) return;

        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        var environment = appSettings.Environment.ToString();
        var contextName = context.GetType().FullName!;

        var migrations = context.Database.GetMigrations().ToArray();
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        var hasPendingModelChanges = context.Database.HasPendingModelChanges();
        var optionsAttributes = DbContextConventions.GetContextAttributes(context);
        var usePrototypeModeWhenMigrationExists = optionsAttributes.Any(a => a.UsePrototypeModeWhenMigrationExists);
        var changeModel = new DbContextChangeModel(migrations, appliedMigrations, hasPendingModelChanges, usePrototypeModeWhenMigrationExists);

        scopedLog?.AddToActions($"{contextName} has {migrations.Length} migrations");
        scopedLog?.AddToActions($"{contextName} has {appliedMigrations.Length} applied migrations. Last applied: {changeModel.LastAppliedMigration}");
        scopedLog?.AddToActions($"{contextName} has {changeModel.PendingMigrations.Length} pending migrations. Last pending: {changeModel.LastPendingMigration}");
        scopedLog?.AddToActions($"{contextName} has {migrations.Length} total migrations");
        scopedLog?.AddToActions($"{contextName} has has pending model changes");

        var migrate = appSettings is { IsDevEnvironment: true, Features.AutoMigrateDevEnvironment: true };
        if (!migrate)
        {
            scopedLog?.AddToActions($"{contextName} auto migration disabled in {environment}");
            return;
        }

        if (!changeModel.HasPendingChanges)
        {
            if (appSettings.Features.PrototypingMode)
                scopedLog?.AddToActions($"existing {contextName} db is used for prototyping mode since there is no pending changes");
            return;
        }


        if (appSettings.Features.PrototypingMode && changeModel.HasPendingModelChangesForPrototypingMode)
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