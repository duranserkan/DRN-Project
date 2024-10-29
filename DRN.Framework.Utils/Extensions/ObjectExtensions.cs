using System.Diagnostics;
using System.Reflection;

namespace DRN.Framework.Utils.Extensions;

public static class ObjectExtensions
{
    /// <summary>
    /// Finds all properties of a given subtype within a class instance, including nested complex types, and groups them by the containing object instance.
    /// </summary>
    /// <param name="baseType">The base class or interface type to search for.</param>
    /// <param name="instance">The instance of the main class to get properties from.</param>
    /// <returns>A dictionary where each key is an object instance, and each value is a set of PropertyInfo objects for properties that are subtypes of baseType.</returns>
    public static Dictionary<object, ISet<PropertyInfo>> GetGroupedPropertiesOfSubtype(this object instance, Type baseType)
    {
        var groupedProperties = new Dictionary<object, ISet<PropertyInfo>>();
        baseType.FindPropertiesOfSubtype(instance, groupedProperties, []);
        return groupedProperties;
    }

    private static void FindPropertiesOfSubtype(this Type baseType, object? instance,
        Dictionary<object, ISet<PropertyInfo>> result,
        HashSet<object> visitedInstances)
    {
        if (instance == null || !visitedInstances.Add(instance))
            return;

        var type = instance.GetType();
        var propertiesOfCurrentInstance = new HashSet<PropertyInfo>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propertyValue = property.GetValue(instance);

            // Check if the property type is a subtype of the base type
            if (baseType.IsAssignableFrom(property.PropertyType) && property.PropertyType != baseType)
                propertiesOfCurrentInstance.Add(property);

            // If the property is a complex type, recursively search its properties
            if (propertyValue != null && !property.PropertyType.IsPrimitive && property.PropertyType != typeof(string) && !property.PropertyType.IsEnum)
                FindPropertiesOfSubtype(baseType, propertyValue, result, visitedInstances);
        }

        if (propertiesOfCurrentInstance.Count <= 0) return;

        if (result.TryGetValue(instance, out var propList))
            foreach (var prop in propertiesOfCurrentInstance)
                propList.Add(prop);
        else
            result[instance] = propertiesOfCurrentInstance;
    }
}