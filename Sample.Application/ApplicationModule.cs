using Microsoft.Extensions.DependencyInjection;
using Sample.Utils;

namespace Sample.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddSampleApplicationServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();
        sc.AddSampleUtils();

        return sc;
    }
}