using Microsoft.Extensions.DependencyInjection;

namespace Sample.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddSampleApplicationServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        return sc;
    }
}