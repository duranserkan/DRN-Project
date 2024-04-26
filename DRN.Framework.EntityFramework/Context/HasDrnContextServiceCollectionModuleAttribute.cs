using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
public class HasDrnContextServiceCollectionModuleAttribute : HasServiceCollectionModuleAttribute
{
    static HasDrnContextServiceCollectionModuleAttribute() =>
        ModuleMethodInfo = typeof(ServiceCollectionExtensions)
            .GetMethod(nameof(ServiceCollectionExtensions.AddDbContextsWithConventions))!;

    public override async Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider)
    {
        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        var migrate = appSettings.Configuration.GetValue(DbContextConventions.AutoMigrateDevEnvironmentKey, false);
        if (appSettings.Environment == AppEnvironment.Development && migrate && service is DbContext context)
            await context.Database.MigrateAsync();
    }
}