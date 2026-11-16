using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Nexus.Utils;

public static class UtilsModule
{
    public static IServiceCollection AddNexusUtils(this IServiceCollection services)
    {
        services.AddServicesWithAttributes();
        return services;
    }
}
