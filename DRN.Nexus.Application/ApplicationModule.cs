using Microsoft.Extensions.DependencyInjection;

namespace DRN.Nexus.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddNexusApplicationServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        return sc;
    }
}