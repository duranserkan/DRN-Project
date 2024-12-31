using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.DependencyInjection;

[Singleton<DrnServiceContainerCollection>]
public class DrnServiceContainerCollection
{
    public DrnServiceContainerCollection(IEnumerable<DrnServiceContainer> serviceContainers)
    {
        ServiceContainers = serviceContainers.ToArray();
        ServiceTypeAndLifetimeMappings = ServiceContainers
            .SelectMany(container => container.LifetimeAttributes)
            .GroupBy(attribute => attribute.ServiceType)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.First());
    }

    public IReadOnlyList<DrnServiceContainer> ServiceContainers { get; }

    public Dictionary<Type, LifetimeAttribute> ServiceTypeAndLifetimeMappings { get; }
}