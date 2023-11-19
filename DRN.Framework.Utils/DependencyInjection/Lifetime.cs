using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public abstract class LifetimeAttribute(ServiceLifetime serviceLifetime, Type serviceType, bool tryAdd, object? key)
    : Attribute
{
    public ServiceLifetime ServiceLifetime { get; } = serviceLifetime;
    public Type ServiceType { get; } = serviceType;
    public Type ImplementationType { get; internal set; } = serviceType; //will be overridden by implementation when assembly scanning performed
    public bool TryAdd { get; } = tryAdd;
    public object? Key { get; } = key;
    public bool HasKey => Key != null;

    public static bool HasLifetime(Type type) => type is { IsAbstract: false, IsClass: true, IsVisible: true } &&
                                                 type.GetCustomAttributes().Any(a => a.GetType().IsAssignableTo(typeof(LifetimeAttribute)));

    public static LifetimeAttribute GetLifetime(Type type) =>
        (LifetimeAttribute)type.GetCustomAttributes().Single(a => a.GetType().IsAssignableTo(typeof(LifetimeAttribute)));
}

public class LifetimeAttribute<TService>(ServiceLifetime serviceLifetime, bool tryAdd = true, object? key = null)
    : LifetimeAttribute(serviceLifetime, typeof(TService), tryAdd, key);

public class LifetimeWithKeyAttribute<TService>(ServiceLifetime serviceLifetime, object key, bool tryAdd = true)
    : LifetimeAttribute(serviceLifetime, typeof(TService), tryAdd, key);

public class ScopedAttribute<TService>(bool tryAdd = true) : LifetimeAttribute<TService>(ServiceLifetime.Scoped, tryAdd);

public class ScopedWithKeyAttribute<TService>(object key, bool tryAdd = true) : LifetimeWithKeyAttribute<TService>(ServiceLifetime.Scoped, key, tryAdd);

public class TransientAttribute<TService>(bool tryAdd = true) : LifetimeAttribute<TService>(ServiceLifetime.Transient, tryAdd);

public class TransientWithKeyAttribute<TService>(object key, bool tryAdd = true) : LifetimeWithKeyAttribute<TService>(ServiceLifetime.Transient, key, tryAdd);

public class SingletonAttribute<TService>(bool tryAdd = true) : LifetimeAttribute<TService>(ServiceLifetime.Singleton, tryAdd);

public class SingletonWithKeyAttribute<TService>(object key, bool tryAdd = true) : LifetimeWithKeyAttribute<TService>(ServiceLifetime.Singleton, key, tryAdd);