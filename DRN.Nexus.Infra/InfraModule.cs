using DRN.Nexus.Domain.NexusIdentity;
using DRN.Nexus.Infra.NexusIdentity;
using DRN.Nexus.Utils;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Nexus.Infra;

public static class InfraModule
{
    public static IServiceCollection AddNexusInfraServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();
        sc.AddNexusUtils();

        sc.AddIdentityCore<NexusUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<NexusIdentityContext>();

        return sc;
    }
}
