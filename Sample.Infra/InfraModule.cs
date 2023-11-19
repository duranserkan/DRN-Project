using Microsoft.Extensions.DependencyInjection;

namespace Sample.Infra;

public static class InfraModule
{
    public static IServiceCollection AddInfraServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        return sc;
    }
}