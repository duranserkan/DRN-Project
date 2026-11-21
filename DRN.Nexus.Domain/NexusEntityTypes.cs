using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.SharedKernel.Domain;

namespace DRN.Nexus.Domain;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NexusEntityTypeAttribute(NexusEntityTypes entityType)
    : EntityTypeAttribute<NexusApp>((byte)entityType);

[DomainOnly]
public enum NexusEntityTypes : byte
{
    User = 1
}
