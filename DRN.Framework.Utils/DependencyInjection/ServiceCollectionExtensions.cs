using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private static readonly ConditionalWeakTable<Assembly, AssemblyScanMetadata> AssemblyScanMetadataCache = new();

    /// <summary>
    /// Scans implementations with LifetimeAttributes in the calling assembly and adds them to the service collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static DrnServiceContainer AddServicesWithAttributes(this IServiceCollection sc)
        => sc.AddServicesWithAttributes(Assembly.GetCallingAssembly());

    /// <summary>
    /// Scans implementations with LifetimeAttributes in the specified assembly and adds them to the service collection.
    /// </summary>
    public static DrnServiceContainer AddServicesWithAttributes(this IServiceCollection sc, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (assembly != typeof(AppSettings).Assembly)
            sc.AddDrnUtils();

        var container = CreateDrnServiceContainer(assembly);
        return container.AddServices(sc);
    }

    private static DrnServiceContainer CreateDrnServiceContainer(Assembly assembly)
    {
        var metadata = AssemblyScanMetadataCache.GetValue(assembly, static scannedAssembly =>
        {
            var types = scannedAssembly.GetTypes();
            var lifetimeTypes = types
                .Where(type => LifetimeAttribute.HasLifetime(type) && !ServiceRegistrationAttribute.HasServiceCollectionModule(type))
                .ToArray();
            var serviceRegistrationTypes = types
                .Where(ServiceRegistrationAttribute.HasServiceCollectionModule)
                .ToArray();

            return new AssemblyScanMetadata(Array.AsReadOnly(lifetimeTypes), Array.AsReadOnly(serviceRegistrationTypes));
        });

        var lifetimeAttributes = metadata.LifetimeTypes.Select(LifetimeAttribute.GetLifetime).ToArray();

        return new DrnServiceContainer(assembly, lifetimeAttributes, metadata.ServiceRegistrationTypes);
    }

    private sealed record AssemblyScanMetadata(ReadOnlyCollection<Type> LifetimeTypes, ReadOnlyCollection<Type> ServiceRegistrationTypes);
}