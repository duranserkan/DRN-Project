using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Nexus.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        return sc;
    }
}