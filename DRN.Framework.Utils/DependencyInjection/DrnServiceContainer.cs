using System.Reflection;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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

            if (HandleSpecialLifetimes(sc, lifetime)) continue;

            if (lifetime.TryAdd)
                sc.TryAdd(descriptor);
            else
                sc.Add(descriptor);
        }
    }

    private static bool HandleSpecialLifetimes(IServiceCollection sc, LifetimeAttribute lifetime)
    {
        if (lifetime is HostedServiceAttribute)
        {
            if (!lifetime.ImplementationType.IsAssignableTo(typeof(IHostedService))) return true;

            var extensionClass = typeof(ServiceCollectionHostedServiceExtensions);
            var extensionMethod = nameof(ServiceCollectionHostedServiceExtensions.AddHostedService);

            extensionClass.InvokeStaticGenericMethod(extensionMethod, [lifetime.ImplementationType], sc);

            return true;
        }

        if (lifetime is ConfigAttribute ca)
        {
            sc.TryAddSingleton(lifetime.ImplementationType, sp =>
            {
                var appSettings = sp.GetRequiredService<IAppSettings>();
                try
                {
                    var configObject = appSettings.InvokeGenericMethod(nameof(IAppSettings.Get), [lifetime.ImplementationType],
                        ca.ConfigKey, !string.IsNullOrEmpty(ca.ConfigKey) && ca.ErrorOnUnknownConfiguration, ca.BindNonPublicProperties);
                    return configObject!;
                }
                catch (TargetInvocationException e)
                {
                    if (e.InnerException != null)
                        throw e.InnerException;
                    throw;
                }
            });

            return true;
        }

        return false;
    }

    private void AddAttributeSpecifiedModules(IServiceCollection serviceCollection)
    {
        var moduleAttributes = Assembly.GetTypes()
            .Where(ServiceRegistrationAttribute.HasServiceCollectionModule)
            .Select(ServiceRegistrationAttribute.GetModuleAttribute)
            .Distinct().ToArray();

        foreach (var moduleAttribute in moduleAttributes)
        {
            var moduleCollection = new ServiceCollection();
            moduleAttribute.ServiceRegistration(moduleCollection, Assembly);
            moduleAttribute.ServiceRegistration(serviceCollection, Assembly);
            var attributeModule = new AttributeSpecifiedServiceModule(moduleCollection, moduleAttribute);
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