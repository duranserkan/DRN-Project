using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.Utils.Extensions;

public static class ServiceCollectionExtensions
{
    public static ServiceDescriptor[] GetAllAssignableTo<TService>(this IServiceCollection sc)
    {
        var target = typeof(TService);
        return sc.Where(d => d.ServiceType.IsAssignableTo(target)).ToArray();
    }

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
}