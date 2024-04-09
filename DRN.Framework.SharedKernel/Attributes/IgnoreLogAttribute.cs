using System.Reflection;

namespace DRN.Framework.SharedKernel.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
public class IgnoreLogAttribute : Attribute;

public static class IgnoreLogExtensions
{
    public static bool IgnoredLog(this object obj) => obj.GetType().GetCustomAttributes()
        .Any(attribute => attribute.GetType() == typeof(IgnoreLogAttribute));

    public static bool IgnoredLog(this PropertyInfo info) =>
        info.PropertyType == typeof(object) ||
        info.GetCustomAttributes().Union(info.PropertyType.GetCustomAttributes())
            .Any(attribute => attribute.GetType() == typeof(IgnoreLogAttribute));
}