using Microsoft.Extensions.DependencyInjection;

namespace DRN.Nexus.Infra;

public static class InfraModule
{
    public static IServiceCollection AddNexusInfraServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        return sc;
    }
}