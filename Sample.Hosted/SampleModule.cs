using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Identity;
using Sample.Infra.Identity;

namespace Sample.Hosted;

public static class SampleModule
{
    public static IServiceCollection AddSampleServices(this IServiceCollection services, IAppSettings appSettings)
    {
        services.AddServicesWithAttributes();
        services.AddIdentityApiEndpoints<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddDefaultTokenProviders()
            .AddEntityFrameworkStores<SampleIdentityContext>();
        //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity

        return services;
    }
}