using DRN.Framework.SharedKernel.Domain;

namespace DRN.Nexus.Domain;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NexusEntityTypeAttribute(NexusEntityTypes entityType)
    : EntityTypeAttribute<NexusApp>((byte)entityType);

public enum NexusEntityTypes : byte
{
    User = 1
}
