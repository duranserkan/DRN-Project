namespace DRN.Framework.SharedKernel.Domain;

public readonly record struct SourceKnownId(long Id, DateTimeOffset CreatedAt, uint InstanceId, byte AppId, byte AppInstanceId) : IComparable<SourceKnownId>
{
    public bool Equals(SourceKnownId other) => Id == other.Id;
    public override int GetHashCode() => Id.GetHashCode();
    public int CompareTo(SourceKnownId other) => Id.CompareTo(other.Id);

    public static bool operator >(SourceKnownId left, SourceKnownId right) => left.Id > right.Id;
    public static bool operator <(SourceKnownId left, SourceKnownId right) => left.Id < right.Id;
    public static bool operator >=(SourceKnownId left, SourceKnownId right) => left.Id >= right.Id;
    public static bool operator <=(SourceKnownId left, SourceKnownId right) => left.Id <= right.Id;
}

public readonly record struct SourceKnownEntityId(SourceKnownId Source, Guid EntityId, byte EntityTypeId, bool Valid) : IComparable<SourceKnownEntityId>
{
    public bool HasSameEntityType(SourceKnownEntityId other) => EntityTypeId == other.EntityTypeId;
    public bool HasSameEntityType<TEntity>() where TEntity : SourceKnownEntity
        => EntityTypeId == SourceKnownEntity.GetEntityTypeId<TEntity>();

    public bool Equals(SourceKnownEntityId other) => EntityId == other.EntityId;
    public override int GetHashCode() => EntityId.GetHashCode();
    public int CompareTo(SourceKnownEntityId other) => EntityTypeId == other.EntityTypeId ? Source.CompareTo(other.Source) : 1;

    public static bool operator >(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) > 0;
    public static bool operator <(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) < 0;
    public static bool operator >=(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) >= 0;
    public static bool operator <=(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) <= 0;
}