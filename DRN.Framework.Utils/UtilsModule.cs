using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils;

public static class UtilsModule
{
    public static IServiceCollection AddDrnUtils(this IServiceCollection collection)
    {
        collection.AddServicesWithAttributes();

        return collection;
    }
}