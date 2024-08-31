using DRN.Framework.Utils.Settings;
using DRN.Nexus.Infra.Identity;
using Microsoft.AspNetCore.Identity;

namespace DRN.Nexus.Hosted;

public static class NexusModule
{
    public static IServiceCollection AddNexusServices(this IServiceCollection services, IAppSettings appSettings)
    {
        services.AddServicesWithAttributes();

        services.AddIdentityApiEndpoints<IdentityUser>()
            .AddEntityFrameworkStores<NexusIdentityContext>();

        return services;
    }
}