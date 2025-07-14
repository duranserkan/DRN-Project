using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public static class ServiceProviderExtensions
{
    private static readonly HashSet<Type> ServicesWithMultipleImplementations = new();

    /// <summary>
    /// Resolves all services registered by attributes to make sure they are resolvable at startup time.
    /// </summary>
    public static void ValidateServicesAddedByAttributes(this IServiceProvider rootServiceProvider, IScopedLog? scopedLog = null, Func<LifetimeAttribute, bool>? ignore = null)
    {
        using var scope = rootServiceProvider.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();

        if (appSettings.Features.ApplicationStartedBy != null)
            scopedLog?.Add(nameof(appSettings.Features.ApplicationStartedBy), appSettings.Features.ApplicationStartedBy);

        if (appSettings.Features.SkipValidation)
            return;

        var containers = serviceProvider.GetServices<DrnServiceContainer>().ToArray();
        var lifetimeAttributes = containers.SelectMany(container => container.LifetimeAttributes).ToArray();
        var attributeSpecifiedModules = containers.SelectMany(container => container.AttributeSpecifiedModules).ToArray();

        foreach (var attribute in lifetimeAttributes)
        {
            if (HandleSpecialCases(ignore, attribute, serviceProvider)) continue;

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
                module.ModuleAttribute.PostStartupValidationAsync(service, serviceProvider, scopedLog).GetAwaiter().GetResult();
            }
        }
    }

    private static bool HandleSpecialCases(Func<LifetimeAttribute, bool>? ignore, LifetimeAttribute attribute, IServiceProvider serviceProvider)
    {
        if (ignore?.Invoke(attribute) ?? false)
            return true;

        if (attribute is ConfigAttribute ca)
        {
            var config = serviceProvider.GetRequiredService(attribute.ImplementationType);
            if (ca.ValidateAnnotations)
                config.ValidateDataAnnotationsThrowIfInvalid(
                    $"Startup Validation for {attribute.ImplementationType.FullName} triggered by {nameof(ConfigAttribute)}.{nameof(ConfigAttribute.ValidateAnnotations)}");
            return true;
        }

        return false;
    }
}