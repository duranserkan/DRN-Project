using System.Reflection;

namespace DRN.Framework.Utils.Extensions;

public static class TypeExtensions
{
    public static Type[] GetTypesAssignableTo(this Assembly? assembly, Type to) => assembly?.GetTypes().Where(t => t.IsAssignableTo(to)).ToArray() ?? [];
}