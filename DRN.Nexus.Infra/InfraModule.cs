using DRN.Nexus.Infra.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Nexus.Infra;

public static class InfraModule
{
    public static IServiceCollection AddNexusInfraServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        sc.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<NexusIdentityContext>();

        return sc;
    }
}