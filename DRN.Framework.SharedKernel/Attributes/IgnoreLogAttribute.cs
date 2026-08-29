using System.Collections.Concurrent;
using System.Reflection;

namespace DRN.Framework.SharedKernel.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
public class IgnoreLogAttribute : Attribute;

public static class IgnoreLogExtensions
{
    private static readonly ConcurrentDictionary<Type, bool> TypeIgnoredCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, bool> PropertyIgnoredCache = new();

    public static bool IgnoredLog(this object? obj)
    {
        if (obj == null) return false;
        return TypeIgnoredCache.GetOrAdd(obj.GetType(), static type =>
            type.GetCustomAttribute<IgnoreLogAttribute>() != null);
    }

    public static bool IgnoredLog(this PropertyInfo info) =>
        PropertyIgnoredCache.GetOrAdd(info, static prop =>
            prop.PropertyType == typeof(object) ||
            prop.GetCustomAttribute<IgnoreLogAttribute>() != null ||
            prop.PropertyType.GetCustomAttribute<IgnoreLogAttribute>() != null);
}