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

/// <summary>
/// Core entity ID operations available to domain entities in SharedKernel.
/// Implemented by <c>SourceKnownEntityIdUtils</c> in Utils; injected into entities by EF interceptors.
/// Parse and Validate use the configured format when omitted; explicit formats override it.
/// </summary>
public interface ISourceKnownEntityIdOperations
{
    SourceKnownEntityId Generate(long id, EntityTypeId entityTypeId);
    /// <summary>Parses the selected format; null uses the implementation's configured default.</summary>
    SourceKnownEntityId Parse(Guid entityId, SourceKnownEntityIdFormat? format = null);
    /// <summary>Parses the selected format; null input remains null. Undefined formats throw.</summary>
    SourceKnownEntityId? Parse(Guid? entityId, SourceKnownEntityIdFormat? format = null);

    /// <summary>Validates integrity and entity type against the configured application partition.</summary>
    SourceKnownEntityId Validate(Guid entityId, byte entityType, SourceKnownEntityIdFormat? format = null);
    SourceKnownEntityId? Validate(Guid? entityId, byte entityType, SourceKnownEntityIdFormat? format = null);
    /// <summary>Validates integrity and entity type against the explicit application partition.</summary>
    SourceKnownEntityId Validate<TApp>(Guid entityId, byte entityType, SourceKnownEntityIdFormat? format = null) where TApp : IAppId;
    SourceKnownEntityId? Validate<TApp>(Guid? entityId, byte entityType, SourceKnownEntityIdFormat? format = null) where TApp : IAppId;
    /// <summary>Validates integrity and both explicitly supplied identity components.</summary>
    SourceKnownEntityId Validate(Guid entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat? format = null);
    SourceKnownEntityId? Validate(Guid? entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat? format = null);
    /// <summary>Validates integrity against the entity's declared identity.</summary>
    SourceKnownEntityId Validate<TEntity>(Guid entityId, SourceKnownEntityIdFormat? format = null) where TEntity : SourceKnownEntity;
    SourceKnownEntityId? Validate<TEntity>(Guid? entityId, SourceKnownEntityIdFormat? format = null) where TEntity : SourceKnownEntity;

    SourceKnownEntityId ToSecure(SourceKnownEntityId id);
    SourceKnownEntityId ToPlain(SourceKnownEntityId id);
}

public readonly record struct SourceKnownEntityId(SourceKnownId Source, Guid EntityId, byte EntityType, bool Valid, bool Secure)
    : IComparable<SourceKnownEntityId>
{
    public EntityTypeId EntityTypeId => new(EntityType, Source.AppId);

    public bool HasSameEntityType(SourceKnownEntityId other) => HasSameEntityTypeId(other.EntityTypeId);
    public bool HasSameEntityType<TEntity>() where TEntity : SourceKnownEntity => HasSameEntityTypeId(SourceKnownEntity.GetEntityTypeId<TEntity>());

    public bool HasSameEntityTypeId(EntityTypeId other) => EntityTypeId == other;
    public bool HasSameEntityTypeId(SourceKnownEntityId other) => HasSameEntityTypeId(other.EntityTypeId);
    public bool HasSameEntityTypeId<TEntity>() where TEntity : SourceKnownEntity => HasSameEntityTypeId(SourceKnownEntity.GetEntityTypeId<TEntity>());

    /// <summary>Checks stored validity and an optional format, without reauthenticating the GUID. Null or Auto accepts either format.</summary>
    public void ValidateId(SourceKnownEntityIdFormat? format = null)
    {
        ValidateFormat(format);
        if (!Valid || (format == SourceKnownEntityIdFormat.Secure && !Secure) || (format == SourceKnownEntityIdFormat.Plain && Secure))
            throw ExceptionFor.Validation($"Invalid EntityId: {EntityId}");
    }

    internal static void ValidateFormat(SourceKnownEntityIdFormat? format)
    {
        if (format is not (null or SourceKnownEntityIdFormat.Secure or SourceKnownEntityIdFormat.Plain or SourceKnownEntityIdFormat.Auto))
            throw new ArgumentOutOfRangeException(nameof(format), format, "Undefined entity ID format.");
    }

    /// <summary>Checks stored validity, optional format and both identity components against the entity declaration.</summary>
    public void Validate<TEntity>(SourceKnownEntityIdFormat? format = null) where TEntity : SourceKnownEntity
        => Validate(SourceKnownEntity.GetEntityTypeId<TEntity>(), format);

    /// <summary>Checks stored validity, optional format and both explicitly supplied identity components.</summary>
    public void Validate(EntityTypeId expected, SourceKnownEntityIdFormat? format = null)
    {
        ValidateId(format);
        if (HasSameEntityTypeId(expected))
            return;

        var expectedType = SourceKnownEntity.GetEntityType(expected);
        var expectedName = expectedType == null ? expected.ToString() : expectedType.FullName ?? expectedType.Name;

        var actualType = SourceKnownEntity.GetEntityType(EntityTypeId);
        var actualName = actualType == null ? EntityTypeId.ToString() : actualType.FullName ?? actualType.Name;

        var ex = new ValidationException($"Invalid Entity Type: EntityId:{EntityId:N}");
        ex.Data.Add($"Expected_{nameof(EntityType)}", expectedName);
        ex.Data.Add($"Found_{nameof(EntityType)}", actualName);

        throw ex;
    }

    public bool Equals(SourceKnownEntityId other) => EntityId == other.EntityId;
    public override int GetHashCode() => EntityId.GetHashCode();

    public int CompareTo(SourceKnownEntityId other)
    {
        var typeComparison = EntityTypeId.CompareTo(other.EntityTypeId);
        return typeComparison != 0 ? typeComparison : Source.CompareTo(other.Source);
    }

    public static bool operator >(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) > 0;
    public static bool operator <(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) < 0;
    public static bool operator >=(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) >= 0;
    public static bool operator <=(SourceKnownEntityId left, SourceKnownEntityId right) => left.CompareTo(right) <= 0;
}
