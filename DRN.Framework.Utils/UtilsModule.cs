using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.Logging.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.Utils;

public static class UtilsModule
{
    public static IServiceCollection AddDrnUtils(this IServiceCollection collection)
    {
        collection.AddServicesWithAttributes();
        collection.AddHybridCache(); //todo: evaluate fusion cache
        collection.AddRedaction(builder => builder.SetRedactor<DiagnosticRedactor>(
            DataClassifications.Sensitive, DataClassifications.Personal, DataClassifications.Set));
        collection.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);

        return collection;
    }
}
