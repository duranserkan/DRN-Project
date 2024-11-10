using System.Reflection;
using DRN.Framework.EntityFramework.Extensions;
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
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
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
        var pendingMigrations = migrations.Except(appliedMigrations).ToArray();
        var lastApplied = appliedMigrations.LastOrDefault() ?? "n/a";
        var lastPending = pendingMigrations.LastOrDefault() ?? "n/a";

        scopedLog?.AddToActions($"{contextName} has {migrations.Length} migrations");
        scopedLog?.AddToActions($"{contextName} has {appliedMigrations.Length} applied migrations. Last applied: {lastApplied}");
        scopedLog?.AddToActions($"{contextName} has {pendingMigrations.Length} pending migrations. Last pending: {lastPending}");
        
        var migrate = appSettings.IsDevEnvironment && appSettings.Features.AutoMigrateDevEnvironment;
        if (!migrate)
        {
            scopedLog?.AddToActions($"{contextName} auto migration disabled in {environment}");
            return;
        }

        scopedLog?.AddToActions($"{contextName} is migrating {environment}");
        await context.Database.MigrateAsync();
        scopedLog?.AddToActions($"{contextName} migrated {pendingMigrations.Length} pending migrations");
    }
}