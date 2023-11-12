using System.Reflection;
using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.Utils.Extensions;

public static class ServiceCollectionExtensions
{
    public static void ReplaceInstance<TImplementation>(this IServiceCollection sc, Type serviceType, IReadOnlyList<TImplementation> implementations,
        ServiceLifetime lifetime)
        where TImplementation : class
    {
        sc.RemoveAll(serviceType);
        var descriptors = implementations.Select(i => new ServiceDescriptor(serviceType, sp => i, lifetime));
        sc.Add(descriptors);
    }

    public static void ReplaceTransient<TService, TImplementation>(this IServiceCollection sc, TImplementation implementation)
        where TService : class
        where TImplementation : class, TService
    {
        sc.RemoveAll<TService>();
        sc.AddTransient<TService, TImplementation>(sp => implementation);
    }


    public static void ReplaceScoped<TService, TImplementation>(this IServiceCollection sc, TImplementation implementation)
        where TService : class
        where TImplementation : class, TService
    {
        sc.RemoveAll<TService>();
        sc.AddScoped<TService, TImplementation>(sp => implementation);
    }

    public static void ReplaceSingleton<TService, TImplementation>(this IServiceCollection sc, TImplementation implementation)
        where TService : class
        where TImplementation : class, TService
    {
        sc.RemoveAll<TService>();
        sc.AddSingleton<TService, TImplementation>(sp => implementation);
    }

    /// <summary>
    /// This method scans implementations with LifetimeAttributes and adds them to the service collection
    /// Method needs to be called from assembly to scan
    /// </summary>
    public static LifetimeContainer AddServiceWithLifetimeAttributes(this IServiceCollection sc)
    {
        var assembly = Assembly.GetCallingAssembly();
        var lifetimeAttributes = assembly.GetTypes().Where(LifetimeAttribute.HasLifetime).Select(t =>
            {
                var lifetime = LifetimeAttribute.GetLifetime(t);
                lifetime.ImplementationType = t;

                var descriptor = new ServiceDescriptor(lifetime.ServiceType, t, lifetime.ServiceLifetime);
                if (lifetime.TryAdd)
                    sc.TryAdd(descriptor);
                else
                    sc.Add(descriptor);

                return lifetime;
            }).ToArray();

        var container = new LifetimeContainer(assembly, lifetimeAttributes);
        sc.AddSingleton(container);

        return container;
    }
}