using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Settings;

namespace Sample.Hosted;

public static class SampleModule
{
    public static IServiceCollection AddSampleServices(this IServiceCollection services, IAppSettings appSettings)
    {
        services.AddServicesWithAttributes();
        if (appSettings.Environment != AppEnvironment.Development) return services;

        services.AddSwaggerGen();

        return services;
    }
}