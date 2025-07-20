using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.Utils;

public static class UtilsModule
{
    public static IServiceCollection AddDrnUtils(this IServiceCollection collection)
    {
        collection.AddServicesWithAttributes();
        collection.AddHybridCache(); //todo: evaluate fusion cache
        collection.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);

        return collection;
    }
}