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
public class DrnContextServiceRegistrationAttribute : ServiceRegistrationAttribute
{
    public override void ServiceRegistration(IServiceCollection sc, Assembly? assembly)
        => sc.AddDbContextsWithConventions(assembly);

    public override async Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider, IScopedLog? scopedLog = null)
    {
        if (service is not DbContext context) return;

        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        var environment = appSettings.Environment.ToString();
        var autoMigrate = appSettings.Features.AutoMigrateDevEnvironment;
        var contextName = context.GetType().FullName!;

        var migrate = appSettings.IsDevEnvironment && appSettings.Features.AutoMigrateDevEnvironment;
        if (!migrate)
        {
            scopedLog?.AddToActions($"{contextName} auto migration disabled in {environment}, AutoMigrateDevEnvironment: {autoMigrate}");
            return;
        }

        scopedLog?.AddToActions($"{contextName} migration started in {environment}, AutoMigrateDevEnvironment: {autoMigrate}");
        await context.Database.MigrateAsync();
        scopedLog?.AddToActions($"{contextName} migrated;");
    }
}