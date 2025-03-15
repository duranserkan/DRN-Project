using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils;

public static class UtilsModule
{
    public static IServiceCollection AddDrnUtils(this IServiceCollection collection)
    {
        collection.AddServicesWithAttributes();
        collection.AddHybridCache(); //todo: evaluate fusion cache

        return collection;
    }
}