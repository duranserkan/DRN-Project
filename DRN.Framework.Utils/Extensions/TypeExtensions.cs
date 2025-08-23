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

    public static TBaseType[] CreateSubTypes<TBaseType>(this Assembly? assembly)
        => assembly?.GetSubTypes(typeof(TBaseType)).Select(t =>
        {
            try
            {
                if (!t.IsClass || t.IsAbstract) return null;
                // Try to create an instance - this will fail if no parameterless ctor
                return Activator.CreateInstance(t);
            }
            catch
            {
                return null;
            }
        }).OfType<TBaseType>().ToArray() ?? [];

    /// <summary>
    /// Creates a single instance of a type that derives from or implements the specified base type.
    /// </summary>
    /// <exception cref="InvalidOperationException"> The input sequence contains more than one element.</exception>
    public static TBaseType? CreateSubType<TBaseType>(this Assembly? assembly)
        => assembly.CreateSubTypes<TBaseType>().SingleOrDefault();

    public static Type[] GetTypesAssignableTo(this Assembly? assembly, Type to)
        => assembly?.GetTypes().Where(t => t.IsAssignableTo(to)).ToArray() ?? [];

    public static string GetAssemblyName(this Type type)
    {
        var assemblyName = type.Assembly.GetName();
        return assemblyName.Name ?? assemblyName.FullName;
    }
}