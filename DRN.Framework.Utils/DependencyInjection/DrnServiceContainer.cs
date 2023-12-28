using System.Reflection;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.Utils.DependencyInjection;

public class DrnServiceContainer
{
    public const string SkipValidationKey = $"{nameof(DrnServiceContainer)}_{nameof(SkipValidationKey)}";
    public const string SkipValidation = "true";

    public Assembly Assembly { get; }
    public IReadOnlyList<LifetimeAttribute> LifetimeAttributes { get; }
    public IReadOnlyList<AttributeSpecifiedServiceCollectionModule> AttributeSpecifiedModules { get; } = new List<AttributeSpecifiedServiceCollectionModule>();
    public bool FrameworkAssembly { get; }

    public DrnServiceContainer(Assembly assembly, LifetimeAttribute[] lifetimeAttributes)
    {
        Assembly = assembly;
        LifetimeAttributes = lifetimeAttributes;
        FrameworkAssembly = Assembly.FullName?.StartsWith("DRN.Framework") ?? false;
    }

    internal void AddServices(IServiceCollection sc)
    {
        if (AddedBefore(sc)) return;
        sc.AddSingleton(this);
        AddLifetimesToServiceCollection(sc);
        AddAttributeSpecifiedModules(sc);
    }

    private void AddLifetimesToServiceCollection(IServiceCollection sc)
    {
        foreach (var lifetime in LifetimeAttributes)
        {
            var descriptor = lifetime.HasKey
                ? new ServiceDescriptor(lifetime.ServiceType, lifetime.Key, lifetime.ImplementationType, lifetime.ServiceLifetime)
                : new ServiceDescriptor(lifetime.ServiceType, lifetime.ImplementationType, lifetime.ServiceLifetime);
            if (lifetime.TryAdd)
                sc.TryAdd(descriptor);
            else
                sc.Add(descriptor);
        }
    }

    private void AddAttributeSpecifiedModules(IServiceCollection sc)
    {
        var moduleAttributes = Assembly.GetTypes()
            .Where(HasServiceCollectionModuleAttribute.HasServiceCollectionModule)
            .Select(HasServiceCollectionModuleAttribute.GetModuleAttribute).Distinct().ToArray();
        foreach (var moduleAttribute in moduleAttributes)
        {
            var moduleServiceCollection = new ServiceCollection();
            var methodInfoProperty = moduleAttribute.GetType().GetProperty(nameof(HasServiceCollectionModuleAttribute.ModuleMethodInfo),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
            var methodInfo = (MethodInfo)methodInfoProperty.GetValue(null)!;
            methodInfo.Invoke(null, [moduleServiceCollection, Assembly]);

            var attributeModule = new AttributeSpecifiedServiceCollectionModule(methodInfo, moduleServiceCollection);
            AddAttributeModule(attributeModule);

            sc.Add(moduleServiceCollection);
        }
    }

    private bool AddedBefore(IServiceCollection sc) => sc.Any(x =>
        x.Lifetime == ServiceLifetime.Singleton && x.ServiceType == typeof(DrnServiceContainer) && x.ImplementationInstance == this);

    private void AddAttributeModule(AttributeSpecifiedServiceCollectionModule attributeSpecifiedModule) =>
        ((List<AttributeSpecifiedServiceCollectionModule>)AttributeSpecifiedModules).Add(attributeSpecifiedModule);
}

public class AttributeSpecifiedServiceCollectionModule(MethodInfo methodInfo, IList<ServiceDescriptor> serviceDescriptors)
{
    public MethodInfo MethodInfo { get; } = methodInfo;
    public IReadOnlyList<ServiceDescriptor> ServiceDescriptors { get; } = serviceDescriptors.ToArray();

    protected bool Equals(AttributeSpecifiedServiceCollectionModule other)
    {
        return MethodInfo.Equals(other.MethodInfo);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((AttributeSpecifiedServiceCollectionModule)obj);
    }

    public override int GetHashCode()
    {
        return MethodInfo.GetHashCode();
    }

    public static bool operator ==(AttributeSpecifiedServiceCollectionModule? left, AttributeSpecifiedServiceCollectionModule? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AttributeSpecifiedServiceCollectionModule? left, AttributeSpecifiedServiceCollectionModule? right)
    {
        return !Equals(left, right);
    }
}