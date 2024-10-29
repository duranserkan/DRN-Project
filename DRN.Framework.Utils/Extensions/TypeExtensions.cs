using System.Reflection;

namespace DRN.Framework.Utils.Extensions;

public static class TypeExtensions
{
    /// <summary>
    /// Finds all types that are subclasses of the specified type in the specified assemblies.
    /// </summary>
    /// <param name="baseType">The base class or interface type.</param>
    /// <param name="assemblies">Assemblies to search for subclasses.</param>
    /// <returns>List of types that are subclasses or implementers of the baseType.</returns>
    public static Type[] GetSubTypes(this Assembly? assembly, Type baseType)
        => assembly?.GetTypes().Where(t => t != baseType && t.IsAssignableTo(baseType)).ToArray() ?? [];

    public static Type[] GetTypesAssignableTo(this Assembly? assembly, Type to)
        => assembly?.GetTypes().Where(t => t.IsAssignableTo(to)).ToArray() ?? [];

    public static MethodInfo MakeGenericMethod(this Type type, string methodName, params Type[] genericTypeArguments)
    {
        var genericMethod = type.GetMethod(methodName);
        ArgumentNullException.ThrowIfNull(genericMethod);
        if (!genericMethod.IsGenericMethod) throw new ArgumentException($"{type.FullName}.{methodName} should be generic method");

        var method = genericMethod.MakeGenericMethod(genericTypeArguments);

        return method;
    }

    public static string GetAssemblyName(this Type type)
    {
        var assemblyName = type.Assembly.GetName();
        return assemblyName.Name ?? assemblyName.FullName;
    }
}