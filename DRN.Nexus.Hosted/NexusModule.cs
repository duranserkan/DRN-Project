using DRN.Framework.Utils.Settings;

namespace DRN.Nexus.Hosted;

public static class NexusModule
{
    public static IServiceCollection AddNexusServices(this IServiceCollection services, IAppSettings appSettings)
    {
        services.AddServicesWithAttributes();

        return services;
    }
}