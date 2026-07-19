using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private static readonly ConcurrentDictionary<string, DrnServiceContainer> ContainerDictionary = new();

    /// <summary>
    /// Scans implementations with LifetimeAttributes in the calling assembly and adds them to the service collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static DrnServiceContainer AddServicesWithAttributes(this IServiceCollection sc)
    {
        return sc.AddServicesWithAttributes(Assembly.GetCallingAssembly());
    }

    /// <summary>
    /// Scans implementations with LifetimeAttributes in the specified assembly and adds them to the service collection.
    /// </summary>
    public static DrnServiceContainer AddServicesWithAttributes(this IServiceCollection sc, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (assembly != typeof(AppSettings).Assembly) sc.AddDrnUtils();

        var container = CreateDrnServiceContainer(assembly);
        container.AddServices(sc);

        return container;
    }

    private static DrnServiceContainer CreateDrnServiceContainer(Assembly assembly)
    {
        var container = ContainerDictionary.GetOrAdd(assembly.FullName!, x =>
        {
            var lifetimeAttributes = assembly.GetTypes()
                .Where(type => LifetimeAttribute.HasLifetime(type) && !ServiceRegistrationAttribute.HasServiceCollectionModule(type))
                .Select(LifetimeAttribute.GetLifetime).ToArray();
            var container = new DrnServiceContainer(assembly, lifetimeAttributes);

            return container;
        });

        return container;
    }
}