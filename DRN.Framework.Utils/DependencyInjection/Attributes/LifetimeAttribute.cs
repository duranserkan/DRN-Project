using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public abstract class LifetimeAttribute(ServiceLifetime serviceLifetime, Type serviceType, bool tryAdd, object? key)
    : Attribute
{
    public ServiceLifetime ServiceLifetime { get; } = serviceLifetime;
    public Type ServiceType { get; } = serviceType;
    public Type ImplementationType { get; internal set; } = serviceType; //will be overridden by implementation when assembly scanning performed
    public bool TryAdd { get; } = tryAdd;
    public object? Key { get; } = key;
    public bool HasKey => Key != null;

    public static bool HasLifetime(Type type) =>
        type is { IsAbstract: false, IsClass: true, IsVisible: true } &&
        type.GetCustomAttributes().Any(a => a.GetType().IsAssignableTo(typeof(LifetimeAttribute)));

    public static LifetimeAttribute GetLifetime(Type type)
    {
        var attribute = (LifetimeAttribute)type.GetCustomAttributes().Single(a => a.GetType().IsAssignableTo(typeof(LifetimeAttribute)));
        attribute.ImplementationType = type;

        return attribute;
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class LifetimeAttribute<TService>(ServiceLifetime serviceLifetime, bool tryAdd = true, object? key = null)
    : LifetimeAttribute(serviceLifetime, typeof(TService), tryAdd, key);

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class LifetimeWithKeyAttribute<TService>(ServiceLifetime serviceLifetime, object key, bool tryAdd = true)
    : LifetimeAttribute(serviceLifetime, typeof(TService), tryAdd, key);

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ScopedAttribute<TService>(bool tryAdd = true) : LifetimeAttribute<TService>(ServiceLifetime.Scoped, tryAdd);

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ScopedWithKeyAttribute<TService>(object key, bool tryAdd = true) : LifetimeWithKeyAttribute<TService>(ServiceLifetime.Scoped, key, tryAdd);

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class TransientAttribute<TService>(bool tryAdd = true) : LifetimeAttribute<TService>(ServiceLifetime.Transient, tryAdd);

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class TransientWithKeyAttribute<TService>(object key, bool tryAdd = true) : LifetimeWithKeyAttribute<TService>(ServiceLifetime.Transient, key, tryAdd);

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class SingletonAttribute<TService>(bool tryAdd = true) : LifetimeAttribute<TService>(ServiceLifetime.Singleton, tryAdd);

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class SingletonWithKeyAttribute<TService>(object key, bool tryAdd = true) : LifetimeWithKeyAttribute<TService>(ServiceLifetime.Singleton, key, tryAdd);