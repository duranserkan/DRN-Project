using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Nexus.Infra;

public static class InfraModule
{
    public static IServiceCollection AddInfraServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        return sc;
    }
}