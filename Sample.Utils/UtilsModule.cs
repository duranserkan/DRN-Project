using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.Utils;

public static class UtilsModule
{
    public static IServiceCollection AddSampleUtils(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        return sc;
    }
}