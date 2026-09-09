namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Composite identifier requiring both the entity type and its expected application partition.
/// </summary>
public readonly record struct EntityTypeId(byte EntityType, byte AppId) : IComparable<EntityTypeId>
{
    public int CompareTo(EntityTypeId other)
    {
        var appComparison = AppId.CompareTo(other.AppId);
        return appComparison != 0 ? appComparison : EntityType.CompareTo(other.EntityType);
    }

    public static bool operator >(EntityTypeId left, EntityTypeId right) => left.CompareTo(right) > 0;
    public static bool operator <(EntityTypeId left, EntityTypeId right) => left.CompareTo(right) < 0;
    public static bool operator >=(EntityTypeId left, EntityTypeId right) => left.CompareTo(right) >= 0;
    public static bool operator <=(EntityTypeId left, EntityTypeId right) => left.CompareTo(right) <= 0;
}
