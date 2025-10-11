using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Application wide Unique Entity Type
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EntityTypeAttribute(byte entityType) : Attribute
{
    //todo add roselyn analyzer to check for conflicts and missing attributes
    /// <summary>
    /// Application wide Unique Entity Type
    /// </summary>
    public byte EntityType { get; } = entityType;
}

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
public abstract class SourceKnownEntity(long id = 0) : IHasEntityId, IEquatable<SourceKnownEntity>, IComparable<SourceKnownEntity>
{
    public const int IdColumnOrder = 0;
    public const int ModifiedAtColumnOrder = 1;
    private const string EmptyJson = "{}";

    private static readonly ConcurrentDictionary<Type, byte> TypeToIdMap = new();
    private static readonly ConcurrentDictionary<byte, Type> IdToTypeMap = new();

    public static byte GetEntityType<TEntity>() where TEntity : SourceKnownEntity => GetEntityType(typeof(TEntity));
    public static byte GetEntityType<TEntity>(TEntity entity) where TEntity : SourceKnownEntity => GetEntityType(entity.GetType());

    public static byte GetEntityType(Type entityType) => TypeToIdMap.GetOrAdd(entityType, type =>
    {
        var attribute = type.GetCustomAttribute<EntityTypeAttribute>();
        if (attribute == null)
            throw new InvalidOperationException($"{type.Name} must use {nameof(EntityTypeAttribute)}");

        EnsureUniqueEntityType(type, attribute.EntityType);
        return attribute.EntityType;
    });

    private static void EnsureUniqueEntityType(Type newType, byte newEntityType) =>
        IdToTypeMap.AddOrUpdate( // Thread-safe check-or-add with value factory
            newEntityType,
            addValueFactory: _ => newType,
            updateValueFactory: (entityType, existingType) => existingType != newType
                ? throw new InvalidOperationException($"Entity type value: {entityType} is used by both {existingType.FullName} and {newType.FullName}")
                : existingType);

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
    public SourceKnownEntityId EntityIdSource { get; internal set; }

    [JsonIgnore]
    public bool IsPendingInsert => EntityId == Guid.Empty;

    public string ExtendedProperties { get; set; } = EmptyJson;
    public TModel GetExtendedProperties<TModel>() => JsonSerializer.Deserialize<TModel>(ExtendedProperties)!;
    public void SetExtendedProperties<TModel>(TModel extendedProperty) => ExtendedProperties = JsonSerializer.Serialize(extendedProperty);

    private readonly Lock _idCacheLock = new();
    private Dictionary<Guid, SourceKnownEntityId>? _entityIdCache;
    private Dictionary<byte, Dictionary<long, SourceKnownEntityId>>? _idCache;
    internal Func<long, byte, SourceKnownEntityId>? IdFactory;
    internal Func<Guid, SourceKnownEntityId>? Parser;
    internal Func<SourceKnownEntityId, byte, SourceKnownEntityId>? Validator;

    public SourceKnownEntityId GetEntityId<TEntity>(Guid id, bool cache = false) where TEntity : SourceKnownEntity
        => GetEntityId(id, GetEntityType<TEntity>(), cache);

    public SourceKnownEntityId GetEntityId(Guid id, byte entityType, bool cache = false)
    {
        if (IsPendingInsert)
            throw ExceptionFor.UnprocessableEntity("Current entity with type is not inserted yet. Can not generate Foreign Ids");
        if (Validator == null)
            throw ExceptionFor.Configuration("Validator is not set");

        var sourceKnownId = GetEntityId(id, cache);
        return Validator.Invoke(sourceKnownId, entityType);
    }

    public SourceKnownEntityId GetEntityId(Guid id, bool cache = false)
    {
        if (IsPendingInsert)
            throw ExceptionFor.UnprocessableEntity("Current entity with type is not inserted yet. Can not generate Foreign Ids");
        if (Parser == null)
            throw ExceptionFor.Configuration("Parser is not set");
        if (!cache)
            return Parser.Invoke(id);

        lock (_idCacheLock)
        {
            _entityIdCache ??= new Dictionary<Guid, SourceKnownEntityId>(3);
            if (_entityIdCache.TryGetValue(id, out var existingId))
                return existingId;

            var entityId = Parser.Invoke(id);
            _entityIdCache[id] = entityId;
            return entityId;
        }
    }

    public SourceKnownEntityId GetEntityId<TEntity>(long id, bool cache = false) where TEntity : SourceKnownEntity
        => GetEntityId(GetEntityType<TEntity>(), id, cache);

    public SourceKnownEntityId GetEntityId(byte entityType, long id, bool cache = false)
    {
        if (IsPendingInsert)
            throw ExceptionFor.UnprocessableEntity("Current entity with type is not inserted yet. Can not generate Foreign Ids");
        if (IdFactory == null)
            throw ExceptionFor.Configuration("Id Factory is not set");
        if (!cache)
            return IdFactory.Invoke(id, entityType);

        lock (_idCacheLock)
        {
            SourceKnownEntityId entityId;
            _idCache ??= new Dictionary<byte, Dictionary<long, SourceKnownEntityId>>(3);
            if (_idCache.TryGetValue(entityType, out var entityIds))
            {
                if (entityIds.TryGetValue(id, out var existingId))
                    return existingId;

                entityId = IdFactory.Invoke(id, entityType);
                entityIds[id] = entityId;
                return entityId;
            }

            entityId = IdFactory.Invoke(id, entityType);
            _idCache[entityType] = new Dictionary<long, SourceKnownEntityId>(3)
            {
                [id] = entityId
            };

            return entityId;
        }
    }

    // ReSharper disable once MemberCanBePrivate.Global
    protected void AddDomainEvent(DomainEvent? e)
    {
        if (e != null)
            DomainEvents.Add(e);
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
        if (Equals(other))
            return 0;
        if (other is null || other.Id == 0)
            return 1;
        if (Id == 0)
            return -1;

        return EntityIdSource.HasSameEntityType(other.EntityIdSource)
            ? Id.CompareTo(other.Id)
            : 1;
    }

    public static bool operator ==(SourceKnownEntity? left, SourceKnownEntity? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(SourceKnownEntity? left, SourceKnownEntity? right) => !(left == right);
    public static bool operator >(SourceKnownEntity? left, SourceKnownEntity? right) => Compare(left, right) > 0;
    public static bool operator <(SourceKnownEntity? left, SourceKnownEntity? right) => Compare(left, right) < 0;
    public static bool operator >=(SourceKnownEntity? left, SourceKnownEntity? right) => Compare(left, right) >= 0;
    public static bool operator <=(SourceKnownEntity? left, SourceKnownEntity? right) => Compare(left, right) <= 0;
    private static int Compare(SourceKnownEntity? left, SourceKnownEntity? right) => left?.CompareTo(right) ?? (right is null ? 0 : -1);
}