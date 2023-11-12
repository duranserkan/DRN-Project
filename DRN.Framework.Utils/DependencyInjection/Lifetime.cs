using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public abstract class LifetimeAttribute : Attribute
{
    public LifetimeAttribute(ServiceLifetime serviceLifetime, Type serviceType, bool tryAdd)
    {
        ServiceLifetime = serviceLifetime;
        ServiceType = serviceType;
        TryAdd = tryAdd;
        //will be overridden by implementation when assembly scanning performed
        ImplementationType = serviceType;
    }

    public ServiceLifetime ServiceLifetime { get; }
    public Type ServiceType { get; }
    public bool TryAdd { get; }
    public Type ImplementationType { get; internal set; }

    public static bool HasLifetime(Type type) => type is { IsAbstract: false, IsClass: true, IsVisible: true } &&
                                                 type.GetCustomAttributes().Any(a => a.GetType().IsAssignableTo(typeof(LifetimeAttribute)));

    public static LifetimeAttribute GetLifetime(Type type) =>
        (LifetimeAttribute)type.GetCustomAttributes().Single(a => a.GetType().IsAssignableTo(typeof(LifetimeAttribute)));
}

public class LifetimeAttribute<TService> : LifetimeAttribute
{
    public LifetimeAttribute(ServiceLifetime serviceLifetime, bool tryAdd = true) : base(serviceLifetime, typeof(TService), tryAdd)
    {
    }
}

public class ScopedAttribute<TService> : LifetimeAttribute<TService>
{
    public ScopedAttribute(bool tryAdd = true) : base(ServiceLifetime.Scoped, tryAdd)
    {
    }
}

public class TransientAttribute<TService> : LifetimeAttribute<TService>
{
    public TransientAttribute(bool tryAdd = true) : base(ServiceLifetime.Transient, tryAdd)
    {
    }
}

public class SingletonAttribute<TService> : LifetimeAttribute<TService>
{
    public SingletonAttribute(bool tryAdd = true) : base(ServiceLifetime.Singleton, tryAdd)
    {
    }
}