using System.Reflection;
using DRN.Framework.EntityFramework.Extensions;
using DRN.Framework.Utils.DependencyInjection.Attributes;
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

    public override async Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider)
    {
        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        var migrate = appSettings.Features.AutoMigrateDevEnvironment;
        if (appSettings.IsDevEnvironment && migrate && service is DbContext context)
            await context.Database.MigrateAsync();
    }
}