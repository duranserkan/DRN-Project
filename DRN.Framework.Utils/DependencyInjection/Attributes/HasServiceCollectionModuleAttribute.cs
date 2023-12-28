using System.Reflection;

namespace DRN.Framework.Utils.DependencyInjection.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public abstract class HasServiceCollectionModuleAttribute : Attribute
{
    public static MethodInfo ModuleMethodInfo { get; protected set; } = null!;

    public static bool HasServiceCollectionModule(Type type) =>
        type is { IsAbstract: false, IsClass: true, IsVisible: true } &&
        type.GetCustomAttributes().Any(a => a.GetType().IsAssignableTo(typeof(HasServiceCollectionModuleAttribute)));

    public static HasServiceCollectionModuleAttribute GetModuleAttribute(Type type) =>
        (HasServiceCollectionModuleAttribute)type.GetCustomAttributes().Single(a => a.GetType().IsAssignableTo(typeof(HasServiceCollectionModuleAttribute)));
}