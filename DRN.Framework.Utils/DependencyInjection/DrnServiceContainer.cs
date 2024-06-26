using System.Reflection;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.Utils.DependencyInjection;

public class DrnServiceContainer
{
    public Assembly Assembly { get; }
    public IReadOnlyList<LifetimeAttribute> LifetimeAttributes { get; }
    public IReadOnlyList<AttributeSpecifiedServiceModule> AttributeSpecifiedModules { get; } = new List<AttributeSpecifiedServiceModule>();
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
            .Where(ServiceRegistrationAttribute.HasServiceCollectionModule)
            .Select(ServiceRegistrationAttribute.GetModuleAttribute).Distinct().ToArray();

        foreach (var moduleAttribute in moduleAttributes)
        {
            var initialCollection = new ServiceCollection().Add(sc);
            moduleAttribute.ServiceRegistration(sc, Assembly);
            var moduleDescriptions = sc.Except(initialCollection).ToArray();
            var attributeModule = new AttributeSpecifiedServiceModule(moduleDescriptions, moduleAttribute);
            AddAttributeModule(attributeModule);
        }
    }

    private bool AddedBefore(IServiceCollection sc) => sc.Any(x =>
        x.Lifetime == ServiceLifetime.Singleton && x.ServiceType == typeof(DrnServiceContainer) && x.ImplementationInstance == this);

    private void AddAttributeModule(AttributeSpecifiedServiceModule attributeSpecifiedModule) =>
        ((List<AttributeSpecifiedServiceModule>)AttributeSpecifiedModules).Add(attributeSpecifiedModule);
}

public sealed class AttributeSpecifiedServiceModule(
    IList<ServiceDescriptor> serviceDescriptors,
    ServiceRegistrationAttribute moduleAttribute)
{
    public ServiceRegistrationAttribute ModuleAttribute { get; } = moduleAttribute;
    public IReadOnlyList<ServiceDescriptor> ServiceDescriptors { get; } = serviceDescriptors.ToArray();

    private bool Equals(AttributeSpecifiedServiceModule other)
    {
        return ModuleAttribute.Equals(other.ModuleAttribute);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is AttributeSpecifiedServiceModule other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ModuleAttribute.GetHashCode();
    }

    public static bool operator ==(AttributeSpecifiedServiceModule? left, AttributeSpecifiedServiceModule? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AttributeSpecifiedServiceModule? left, AttributeSpecifiedServiceModule? right)
    {
        return !Equals(left, right);
    }
}