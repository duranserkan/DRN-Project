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
    private readonly List<AttributeSpecifiedServiceModule> _attributeSpecifiedModules = [];
    private readonly IReadOnlyList<Type> _serviceRegistrationTypes;

    public Assembly Assembly { get; }
    public IReadOnlyList<LifetimeAttribute> LifetimeAttributes { get; }
    public IReadOnlyList<AttributeSpecifiedServiceModule> AttributeSpecifiedModules { get; }
    public bool FrameworkAssembly { get; }

    public DrnServiceContainer(Assembly assembly, LifetimeAttribute[] lifetimeAttributes) : this(assembly, lifetimeAttributes,
        assembly.GetTypes().Where(ServiceRegistrationAttribute.HasServiceCollectionModule).ToArray())
    {
    }

    internal DrnServiceContainer(
        Assembly assembly,
        LifetimeAttribute[] lifetimeAttributes,
        IReadOnlyList<Type> serviceRegistrationTypes)
    {
        Assembly = assembly;
        LifetimeAttributes = Array.AsReadOnly(lifetimeAttributes.ToArray());
        _serviceRegistrationTypes = serviceRegistrationTypes;
        AttributeSpecifiedModules = _attributeSpecifiedModules.AsReadOnly();
        FrameworkAssembly = Assembly.FullName?.StartsWith("DRN.Framework") ?? false;
    }

    internal DrnServiceContainer AddServices(IServiceCollection sc)
    {
        var existingContainer = GetExistingContainer(sc);
        if (existingContainer != null)
            return existingContainer;

        sc.AddSingleton(this);
        AddLifetimesToServiceCollection(sc);
        AddAttributeSpecifiedModules(sc);

        return this;
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
            sc.TryAddSingleton(lifetime.ImplementationType, sp => CreateConfigObject(lifetime, sp, ca));

            return true;
        }

        return false;
    }

    private static object CreateConfigObject(LifetimeAttribute lifetime, IServiceProvider sp, ConfigAttribute ca)
    {
        var appSettings = sp.GetRequiredService<IAppSettings>();
        try
        {
            var configKey = ca.ConfigKey ?? ca.ImplementationType.Name;
            var errorOnUnknownConfiguration = configKey != string.Empty && ca.ErrorOnUnknownConfiguration;
            var configObject = appSettings.InvokeGenericMethod(nameof(IAppSettings.Get), [lifetime.ImplementationType],
                configKey, errorOnUnknownConfiguration, ca.BindNonPublicProperties);
            if (configObject != null)
                return configObject;

            try
            {
                configObject = Activator.CreateInstance(lifetime.ImplementationType);
            }
            catch (Exception)
            {
                // ignored
            }

            return configObject ?? throw new ConfigurationException($"ConfigKey: {configKey} is not configured");
        }
        catch (TargetInvocationException e)
        {
            if (e.InnerException != null)
                throw e.InnerException;
            throw;
        }
    }

    private void AddAttributeSpecifiedModules(IServiceCollection serviceCollection)
    {
        var moduleAttributes = _serviceRegistrationTypes
            .Select(ServiceRegistrationAttribute.GetModuleAttribute)
            .Distinct().ToArray();

        foreach (var moduleAttribute in moduleAttributes)
        {
            var moduleCollection = new ServiceCollection();
            moduleAttribute.ServiceRegistration(moduleCollection, Assembly);
            moduleAttribute.ServiceRegistration(serviceCollection, Assembly);
            var attributeModule = new AttributeSpecifiedServiceModule(moduleCollection, moduleAttribute);
            _attributeSpecifiedModules.Add(attributeModule);
        }
    }

    private DrnServiceContainer? GetExistingContainer(IServiceCollection sc) =>
        sc.Where(descriptor =>
                descriptor.Lifetime == ServiceLifetime.Singleton &&
                descriptor.ServiceType == typeof(DrnServiceContainer))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<DrnServiceContainer>()
            .FirstOrDefault(container => ReferenceEquals(container.Assembly, Assembly));
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