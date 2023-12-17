using DRN.Framework.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.Infra;

public static class InfraModule
{
    public static IServiceCollection AddSampleInfraServices(this IServiceCollection sc)
    {
        sc.AddDrnUtils();
        sc.AddServicesWithAttributes();

        return sc;
    }
}