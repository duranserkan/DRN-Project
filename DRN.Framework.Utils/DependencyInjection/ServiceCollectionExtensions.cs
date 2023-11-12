using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.Utils.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private static readonly ConcurrentDictionary<Assembly, LifetimeContainer> ContainerDictionary = new();

    /// <summary>
    /// This method scans implementations with LifetimeAttributes and adds them to the service collection
    /// Method needs to be called from assembly to scan
    /// </summary>
    public static LifetimeContainer AddServicesWithLifetimeAttributes(this IServiceCollection sc, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

        var container = ContainerDictionary.GetOrAdd(assembly, x =>
        {
            var lifetimeAttributes = assembly.GetTypes().Where(LifetimeAttribute.HasLifetime).Select(type =>
            {
                var lifetime = LifetimeAttribute.GetLifetime(type);
                lifetime.ImplementationType = type;

                return lifetime;
            }).ToArray();

            var container = new LifetimeContainer(assembly, lifetimeAttributes);

            return container;
        });

        var addedBefore = sc.Any(x =>
            x.Lifetime == ServiceLifetime.Singleton && x.ServiceType == typeof(LifetimeContainer) & x.ImplementationInstance == container);

        if (addedBefore) return container;
        sc.AddSingleton(container);

        foreach (var lifetime in container.LifetimeAttributes)
        {
            var descriptor = new ServiceDescriptor(lifetime.ServiceType, lifetime.ImplementationType, lifetime.ServiceLifetime);
            if (lifetime.TryAdd)
                sc.TryAdd(descriptor);
            else
                sc.Add(descriptor);
        }

        return container;
    }
}