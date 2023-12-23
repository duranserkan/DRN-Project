using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.Utils.DependencyInjection;

public class LifetimeContainer
{
    public Assembly Assembly { get; }
    public LifetimeAttribute[] LifetimeAttributes { get; }
    public bool FrameworkAssembly { get; }

    public LifetimeContainer(Assembly assembly, LifetimeAttribute[] lifetimeAttributes)
    {
        Assembly = assembly;
        LifetimeAttributes = lifetimeAttributes;
        FrameworkAssembly = Assembly.FullName?.StartsWith("DRN.Framework") ?? false;
    }

    public void AddLifetimesToServiceCollection(IServiceCollection sc)
    {
        if (AddedBefore(sc)) return;

        sc.AddSingleton(this);
        foreach (var lifetime in LifetimeAttributes)
        {
            var descriptor = lifetime.HasKey
                ? new ServiceDescriptor(lifetime.ServiceType, lifetime.Key, lifetime.ImplementationType, lifetime.ServiceLifetime)
                : new ServiceDescriptor(lifetime.ServiceType, lifetime.ImplementationType, lifetime.ServiceLifetime);
            if (lifetime.TryAdd)
                sc.TryAdd(descriptor);
            else
                sc.Add(descriptor);
        }
    }

    private bool AddedBefore(IServiceCollection sc) => sc.Any(x =>
        x.Lifetime == ServiceLifetime.Singleton && x.ServiceType == typeof(LifetimeContainer) && x.ImplementationInstance == this);
}