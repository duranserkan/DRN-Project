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

public readonly record struct SourceKnownEntityId(SourceKnownId Source, Guid EntityId, byte EntityType, bool Valid) : IComparable<SourceKnownEntityId>
{
    public bool HasSameEntityType(byte other) => EntityType == other;
    public bool HasSameEntityType(SourceKnownEntityId other) => HasSameEntityType(other.EntityType);
    public bool HasSameEntityType<TEntity>() where TEntity : SourceKnownEntity => HasSameEntityType(SourceKnownEntity.GetEntityType<TEntity>());

    public void ValidateId()
    {
        if (!Valid)
            throw ExceptionFor.Validation($"Invalid EntityId: {EntityId}");
    }

    public void Validate<TEntity>() where TEntity : SourceKnownEntity => Validate(SourceKnownEntity.GetEntityType<TEntity>());
    public void Validate(byte entityType)
    {
        ValidateId();
        if (HasSameEntityType(entityType))
            return;

        var expectedType = SourceKnownEntity.GetEntityType(entityType);
        var expectedName = expectedType == null ? entityType.ToString() : expectedType.FullName ?? expectedType.Name;

        var actualType = SourceKnownEntity.GetEntityType(EntityType);
        var actualName = actualType == null ? EntityType.ToString() : actualType.FullName ?? actualType.Name;

        var ex = new ValidationException($"Invalid Entity Type: EntityId:{EntityId:N}");
        ex.Data.Add($"Expected_{nameof(EntityType)}", expectedName);
        ex.Data.Add($"Found_{nameof(EntityType)}", actualName);

        throw ex;
    }

    public bool Equals(SourceKnownEntityId other) => EntityId == other.EntityId;
    public override int GetHashCode() => EntityId.GetHashCode();
    public int CompareTo(SourceKnownEntityId other) => EntityType == other.EntityType ? Source.CompareTo(other.Source) : 1;

    public static bool operator >(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) > 0;
    public static bool operator <(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) < 0;
    public static bool operator >=(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) >= 0;
    public static bool operator <=(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) <= 0;
}