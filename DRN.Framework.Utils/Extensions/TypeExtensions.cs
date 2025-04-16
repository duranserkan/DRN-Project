using System.Reflection;

namespace DRN.Framework.Utils.Extensions;

public static class TypeExtensions
{
    /// <summary>
    /// Finds all types that are subclasses of the specified type in the specified assembly.
    /// </summary>
    /// <param name="assembly">Assemblies to search for subclasses.</param>
    /// <param name="baseType">The base class or interface type.</param>
    /// <returns>List of types that are subclasses or implementers of the baseType.</returns>
    public static Type[] GetSubTypes(this Assembly? assembly, Type baseType)
        => assembly?.GetTypes().Where(t => t != baseType && t.IsAssignableTo(baseType)).ToArray() ?? [];

    public static Type[] GetTypesAssignableTo(this Assembly? assembly, Type to)
        => assembly?.GetTypes().Where(t => t.IsAssignableTo(to)).ToArray() ?? [];

    public static string GetAssemblyName(this Type type)
    {
        var assemblyName = type.Assembly.GetName();
        return assemblyName.Name ?? assemblyName.FullName;
    }
}