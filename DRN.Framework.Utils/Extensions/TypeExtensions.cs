using System.Reflection;

namespace DRN.Framework.Utils.Extensions;

public static class TypeExtensions
{
    public static Type[] GetTypesAssignableTo(this Assembly? assembly, Type to) => assembly?.GetTypes().Where(t => t.IsAssignableTo(to)).ToArray() ?? [];

    public static MethodInfo MakeGenericMethod(this Type type, string methodName, params Type[] genericTypeArguments)
    {
        var genericMethod = type.GetMethod(methodName);
        ArgumentNullException.ThrowIfNull(genericMethod);
        if (!genericMethod.IsGenericMethod) throw new ArgumentException($"{type.FullName}.{methodName} should be generic method");

        var method = genericMethod.MakeGenericMethod(genericTypeArguments);

        return method;
    }
}