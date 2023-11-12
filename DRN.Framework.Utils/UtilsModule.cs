using DRN.Framework.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils;

public static class UtilsModule
{
    public static IServiceCollection AddDrnUtils(this IServiceCollection collection)
    {
        collection.AddServiceWithLifetimeAttributes();

        return collection;
    }
}