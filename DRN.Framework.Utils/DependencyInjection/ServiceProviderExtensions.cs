using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public static class ServiceProviderExtensions
{
    private static readonly HashSet<Type> ServicesWithMultipleImplementations = new();

    /// <summary>
    /// Resolves all services registered by attributes to make sure they are resolvable at startup time.
    /// </summary>
    public static void ValidateServicesAddedByAttributes(this IServiceProvider sp)
    {
        var containers = sp.GetServices<LifetimeContainer>().ToArray();
        var attributes = containers.SelectMany(container => container.LifetimeAttributes).ToArray();

        foreach (var attribute in attributes)
        {
            if (attribute.TryAdd)
                sp.GetRequiredService(attribute.ServiceType);
            else if (!ServicesWithMultipleImplementations.Contains(attribute.ServiceType))
            {
                _ = sp.GetServices(attribute.ServiceType).ToArray();
                ServicesWithMultipleImplementations.Add(attribute.ServiceType);
            }
        }
    }
}