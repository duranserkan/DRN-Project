using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public static class ServiceProviderExtensions
{
    private static readonly HashSet<Type> ServicesWithMultipleImplementations = new();

    /// <summary>
    /// Resolves all services registered by attributes to make sure they are resolvable at startup time.
    /// </summary>
    public static void ValidateServicesAddedByAttributes(this IServiceProvider rootServiceProvider)
    {
        using var scope = rootServiceProvider.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        if (appSettings.TryGetSection(DrnServiceContainer.SkipValidationKey, out var configurationSection))
        {
            var dontValidate = configurationSection.Value == DrnServiceContainer.SkipValidation;
            if (dontValidate) return;
        }

        var containers = serviceProvider.GetServices<DrnServiceContainer>().ToArray();
        var lifetimeAttributes = containers.SelectMany(container => container.LifetimeAttributes).ToArray();
        var attributeSpecifiedModules = containers.SelectMany(container => container.AttributeSpecifiedModules).ToArray();

        foreach (var attribute in lifetimeAttributes)
        {
            if (attribute.HasKey)
                serviceProvider.GetRequiredKeyedService(attribute.ServiceType, attribute.Key);
            else if (attribute.TryAdd)
                serviceProvider.GetRequiredService(attribute.ServiceType);
            else if (!ServicesWithMultipleImplementations.Contains(attribute.ServiceType))
            {
                _ = serviceProvider.GetServices(attribute.ServiceType).ToArray();
                ServicesWithMultipleImplementations.Add(attribute.ServiceType);
            }
        }

        foreach (var module in attributeSpecifiedModules)
        {
            foreach (var descriptor in module.ServiceDescriptors)
            {
                var service = descriptor.IsKeyedService
                    ? serviceProvider.GetRequiredKeyedService(descriptor.ServiceType, descriptor.ServiceKey)
                    : serviceProvider.GetRequiredService(descriptor.ServiceType);
                module.ModuleAttribute.PostStartupValidation(service, serviceProvider);
            }
        }
    }
}