namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Composite identifier for an entity type within an application partition.
/// </summary>
public readonly record struct EntityTypeId(byte EntityType, byte AppId = 0) : IComparable<EntityTypeId>
{
    public int CompareTo(EntityTypeId other)
    {
        var appComparison = AppId.CompareTo(other.AppId);
        return appComparison != 0 ? appComparison : EntityType.CompareTo(other.EntityType);
    }

    public static implicit operator EntityTypeId(byte entityType) => new(entityType);

    public static bool operator >(EntityTypeId left, EntityTypeId right) => left.CompareTo(right) > 0;
    public static bool operator <(EntityTypeId left, EntityTypeId right) => left.CompareTo(right) < 0;
    public static bool operator >=(EntityTypeId left, EntityTypeId right) => left.CompareTo(right) >= 0;
    public static bool operator <=(EntityTypeId left, EntityTypeId right) => left.CompareTo(right) <= 0;
}
