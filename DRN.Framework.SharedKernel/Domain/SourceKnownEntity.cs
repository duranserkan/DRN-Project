using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Base EntityType attribute. Must be specialized per application partition.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public abstract class EntityTypeAttribute : Attribute
{
    /// <summary>
    /// Application wide Unique Entity Type
    /// </summary>
    public byte EntityType { get; }

    /// <summary>
    /// Application Identifier (0..127) for domain/application partitioning
    /// </summary>
    public byte AppId { get; }

    protected EntityTypeAttribute(byte entityType, byte appId)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appId, IAppId.MaxAppId);
        EntityType = entityType;
        AppId = appId;
    }
}

/// <summary>
/// Generic EntityType attribute bound to a strongly typed application identifier.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class EntityTypeAttribute<TApp>(byte entityType): EntityTypeAttribute(entityType, TApp.AppId)
    where TApp : IAppId;

public interface IEntityETag
{
    public Guid ETag { get; } //todo generate etag hash(ModifiedAt +EntityId) && implement generic support
}

public interface IHasEntityId
{
    public SourceKnownEntityId EntityIdSource { get; }
}

public interface IEntityWithModel<TModel> where TModel : class
{
    TModel Model { get; set; }
}

/// <summary>
///  <inheritdoc cref="SourceKnownEntity"/>
/// </summary>
public abstract class SourceKnownEntity<TModel>(long id = 0) : SourceKnownEntity(id), IEntityWithModel<TModel> where TModel : class
{
    public TModel Model { get; set; } = null!;
}

/// <summary>
/// Represents the minimum sustainable entity encompassing identity, lifecycle events,
/// and extended property capabilities within the domain model.
/// </summary>
/// <param name="id">Should be a source known id. If not set, DrnContext will provide one on saving changes by default</param>
/// <remarks>
/// The <c>Entity</c> class provides foundational functionality for domain entities,
/// including managing identifiers, domain events, and metadata. It supports equality
/// comparison by reference or identifier and includes mechanisms for state tracking
/// through domain events.
/// </remarks>
[SuppressMessage("SonarQube", "S4035", Justification = "DDD identity equality")]
[SuppressMessage("ReSharper", "StaticMemberInGenericType")]
public abstract class SourceKnownEntity(long id = 0) : IHasEntityId, IEquatable<SourceKnownEntity>, IComparable<SourceKnownEntity>
{
    public const int IdColumnOrder = 0;
    public const int ModifiedAtColumnOrder = 1;
    private const string EmptyJson = "{}";

    private static class EntityMetadataCache<TEntity> where TEntity : SourceKnownEntity
    {
        public static readonly EntityTypeId EntityTypeId = EntityTypeRegistry.GetEntityTypeId(typeof(TEntity));
        public static readonly byte EntityType = EntityTypeId.EntityType;
        public static readonly byte AppId = EntityTypeId.AppId;
    }

    public static Type? GetEntityType(EntityTypeId key) => EntityTypeRegistry.GetEntityType(key);
    public static Type? GetEntityType(byte entityType, byte appId = 0) => EntityTypeRegistry.GetEntityType(new EntityTypeId(entityType, appId));
    public static byte GetEntityType<TEntity>() where TEntity : SourceKnownEntity => EntityMetadataCache<TEntity>.EntityType;
    public static byte GetEntityType<TEntity>(TEntity entity) where TEntity : SourceKnownEntity => GetEntityType(entity.GetType());
    public static byte GetEntityType(Type entityType) => EntityTypeRegistry.GetEntityTypeId(entityType).EntityType;

    public static EntityTypeId GetEntityTypeId<TEntity>() where TEntity : SourceKnownEntity => EntityMetadataCache<TEntity>.EntityTypeId;
    public static EntityTypeId GetEntityTypeId<TEntity>(TEntity entity) where TEntity : SourceKnownEntity => GetEntityTypeId(entity.GetType());
    public static EntityTypeId GetEntityTypeId(Type entityType) => EntityTypeRegistry.GetEntityTypeId(entityType);

    public static byte GetAppId<TEntity>() where TEntity : SourceKnownEntity => EntityMetadataCache<TEntity>.AppId;
    public static byte GetAppId<TEntity>(TEntity entity) where TEntity : SourceKnownEntity => GetAppId(entity.GetType());
    public static byte GetAppId(Type entityType) => EntityTypeRegistry.GetEntityTypeId(entityType).AppId;

    private List<IDomainEvent> DomainEvents { get; } = new(2); //todo transactional outbox, pre and post publish events
    public IReadOnlyList<IDomainEvent> GetDomainEvents() => DomainEvents;

    /// <summary>
    /// Internal use only, Use EntityId for external usage
    /// </summary>
    [JsonIgnore]
    [Column(Order = IdColumnOrder)]
    public long Id { get; internal set; } = id;

    /// <summary>
    /// External use only, don't use Id for external usage
    /// </summary>
    [JsonPropertyName(nameof(Id))]
    [JsonPropertyOrder(-3)]
    public Guid EntityId => EntityIdSource.EntityId;

    [JsonPropertyOrder(-2)]
    public DateTimeOffset CreatedAt => EntityIdSource.Source.CreatedAt;

    [ConcurrencyCheck]
    [JsonPropertyOrder(-1)]
    [Column(Order = ModifiedAtColumnOrder)]
    public DateTimeOffset ModifiedAt { get; protected internal set; }

    [JsonIgnore]
    [NotMapped]
    public SourceKnownEntityId EntityIdSource { get; internal set; }

    [JsonIgnore]
    public bool IsPendingInsert => EntityId == Guid.Empty;

    public string? ExtendedProperties { get; set; } = EmptyJson;
    public TModel? GetExtendedProperties<TModel>() => ExtendedProperties != null ? JsonSerializer.Deserialize<TModel>(ExtendedProperties) : default;

    public void SetExtendedProperties<TModel>(TModel? extendedProperty) where TModel : class
        => ExtendedProperties = extendedProperty != null ? JsonSerializer.Serialize(extendedProperty) : null;

    internal ISourceKnownEntityIdOperations? EntityIdOps;

    private ISourceKnownEntityIdOperations Ops =>
        EntityIdOps ?? throw ExceptionFor.Configuration("EntityId operations are not set");

    public SourceKnownEntityId ToSecure(SourceKnownEntityId id) => Ops.ToSecure(id);
    public SourceKnownEntityId? ToSecure(SourceKnownEntityId? id) => id.HasValue ? Ops.ToSecure(id.Value) : null;
    public SourceKnownEntityId ToPlain(SourceKnownEntityId id) => Ops.ToPlain(id);
    public SourceKnownEntityId? ToPlain(SourceKnownEntityId? id) => id.HasValue ? Ops.ToPlain(id.Value) : null;

    public SourceKnownEntityId GetEntityId<TEntity>(Guid id) where TEntity : SourceKnownEntity => GetEntityId(id, GetEntityTypeId<TEntity>());
    public SourceKnownEntityId? GetEntityId<TEntity>(Guid? id) where TEntity : SourceKnownEntity => GetEntityId(id, GetEntityTypeId<TEntity>());

    public SourceKnownEntityId? GetEntityId(Guid? id, EntityTypeId entityTypeId) => id == null ? null : GetEntityId(id.Value, entityTypeId);

    public SourceKnownEntityId GetEntityId(Guid id, EntityTypeId entityTypeId)
    {
        var sourceKnownId = GetEntityId(id, false);
        sourceKnownId.Validate(entityTypeId);

        return sourceKnownId;
    }

    public SourceKnownEntityId? GetEntityId(Guid? id, byte entityType) => id == null ? null : GetEntityId(id.Value, entityType);

    public SourceKnownEntityId GetEntityId(Guid id, byte entityType)
    {
        var sourceKnownId = GetEntityId(id, false);
        sourceKnownId.Validate(entityType);

        return sourceKnownId;
    }

    public SourceKnownEntityId? GetEntityId(Guid? id, bool validate = true) => id == null ? null : GetEntityId(id.Value, validate);

    public SourceKnownEntityId GetEntityId(Guid id, bool validate = true)
    {
        if (IsPendingInsert)
            throw ExceptionFor.UnprocessableEntity("Current entity with type is not inserted yet. Can not generate Foreign Ids");

        var entityId = Ops.Parse(id);
        if (validate) entityId.ValidateId();

        return entityId;
    }

    public SourceKnownEntityId? GetEntityId<TEntity>(long? id) where TEntity : SourceKnownEntity => GetEntityId(id, GetEntityTypeId<TEntity>());
    public SourceKnownEntityId GetEntityId<TEntity>(long id) where TEntity : SourceKnownEntity => GetEntityId(id, GetEntityTypeId<TEntity>());

    public SourceKnownEntityId? GetEntityId(long? id, EntityTypeId entityTypeId) => id == null ? null : GetEntityId(id.Value, entityTypeId);

    public SourceKnownEntityId GetEntityId(long id, EntityTypeId entityTypeId)
    {
        var sourceKnownId = GetEntityId(id, entityTypeId.EntityType);
        sourceKnownId.Validate(entityTypeId);

        return sourceKnownId;
    }

    public SourceKnownEntityId? GetEntityId(long? id, byte entityType) => id == null ? null : GetEntityId(id.Value, entityType);

    public SourceKnownEntityId GetEntityId(long id, byte entityType) => IsPendingInsert
        ? throw ExceptionFor.UnprocessableEntity("Current entity with type is not inserted yet. Can not generate Foreign Ids")
        : Ops.Generate(id, entityType);

    // ReSharper disable once MemberCanBePrivate.Global
    protected void AddDomainEvent(DomainEvent? e)
    {
        if (e != null) DomainEvents.Add(e);
    }

    internal void MarkAsCreated() => AddDomainEvent(GetCreatedEvent());
    internal void MarkAsModified() => AddDomainEvent(GetModifiedEvent());
    internal void MarkAsDeleted() => AddDomainEvent(GetDeletedEvent());

    protected virtual EntityCreated? GetCreatedEvent() => null;
    protected virtual EntityModified? GetModifiedEvent() => null;
    protected virtual EntityDeleted? GetDeletedEvent() => null;

    public bool Equals(SourceKnownEntity? other) => ReferenceEquals(this, other) || (!IsPendingInsert && EntityIdSource == other?.EntityIdSource);
    public override bool Equals(object? obj) => obj is SourceKnownEntity other && Equals(other);

    // ReSharper disable once NonReadonlyMemberInGetHashCode
    public override int GetHashCode() => EntityIdSource.GetHashCode();

    /// <summary>
    /// Returns comparison result based on Id. Null and Zero-valued ids are considered less than any other id.
    /// </summary>
    /// <returns>
    ///<li>1: if this entity's Id is greater than the other Id, which means this entity is newer than the other.</li>
    ///<li>-1: if this entity's Id is less than the other Id, which means this entity is older than the other.</li>
    ///<li>0: if they are equal, which means they are the same entity.</li>
    /// </returns>
    public int CompareTo(SourceKnownEntity? other)
    {
        if (Equals(other)) return 0;
        if (other is null || other.Id == 0) return 1;
        if (Id == 0) return -1;

        return EntityIdSource.HasSameEntityType(other.EntityIdSource) ? Id.CompareTo(other.Id) : 1;
    }

    public static bool operator ==(SourceKnownEntity? left, SourceKnownEntity? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(SourceKnownEntity? left, SourceKnownEntity? right) => !(left == right);
    public static bool operator >(SourceKnownEntity? left, SourceKnownEntity? right) => Compare(left, right) > 0;
    public static bool operator <(SourceKnownEntity? left, SourceKnownEntity? right) => Compare(left, right) < 0;
    public static bool operator >=(SourceKnownEntity? left, SourceKnownEntity? right) => Compare(left, right) >= 0;
    public static bool operator <=(SourceKnownEntity? left, SourceKnownEntity? right) => Compare(left, right) <= 0;
    private static int Compare(SourceKnownEntity? left, SourceKnownEntity? right) => left?.CompareTo(right) ?? (right is null ? 0 : -1);
}
