using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;

namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Application wide Unique Entity Type Id
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EntityTypeIdAttribute(byte id) : Attribute
{
    //todo add roselyn analyzer to check for conflicts and missing attributes
    /// <summary>
    /// Application wide Unique Entity Type ID
    /// </summary>
    public byte Id { get; } = id;
}

public interface IEntityETag
{
    //todo generate etag hash(ModifiedAt +EntityId) && implement generic support
    public Guid ETag { get; }
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
///  <inheritdoc cref="Entity"/>
/// </summary>
public abstract class Entity<TModel>(long id = 0) : Entity(id), IEntityWithModel<TModel> where TModel : class
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
public abstract class Entity(long id = 0) : IHasEntityId, IEquatable<Entity>, IComparable<Entity>
{
    private const string EmptyJson = "{}";
    private static readonly ConcurrentDictionary<Type, byte> TypeToIdMap = new();

    private static readonly ConcurrentDictionary<byte, Type> IdToTypeMap = new();

    public static byte GetEntityTypeId<TEntity>() where TEntity : Entity => GetEntityTypeId(typeof(TEntity));
    public static byte GetEntityTypeId<TEntity>(TEntity entity) where TEntity : Entity => GetEntityTypeId(entity.GetType());

    public static byte GetEntityTypeId(Type entityType) => TypeToIdMap.GetOrAdd(entityType, type =>
    {
        var attribute = type.GetCustomAttribute<EntityTypeIdAttribute>();
        if (attribute == null)
            throw new InvalidOperationException($"{type.Name} must use {nameof(EntityTypeIdAttribute)}");

        EnsureUniqueId(type, attribute.Id);
        return attribute.Id;
    });

    private static void EnsureUniqueId(Type newType, byte newId) =>
        IdToTypeMap.AddOrUpdate( // Thread-safe check-or-add with value factory
            newId,
            addValueFactory: _ => newType,
            updateValueFactory: (id, existingType) =>
            {
                if (existingType != newType)
                    throw new InvalidOperationException($"ID {id} is used by both {existingType.FullName} and {newType.FullName}");

                return existingType; // No change needed
            });

    private List<IDomainEvent> DomainEvents { get; } = new(2); //todo transactional outbox, pre and post publish events
    public IReadOnlyList<IDomainEvent> GetDomainEvents() => DomainEvents;
    public long Id { get; internal set; } = id;
    public Guid EntityId => EntityIdSource.EntityId;
    public SourceKnownEntityId EntityIdSource { get; internal set; }

    public string ExtendedProperties { get; set; } = EmptyJson;
    public TModel GetExtendedProperties<TModel>() => JsonSerializer.Deserialize<TModel>(ExtendedProperties)!;
    public void SetExtendedProperties<TModel>(TModel extendedProperty) => ExtendedProperties = JsonSerializer.Serialize(extendedProperty);

    public bool IsPendingInsert => EntityId == Guid.Empty;

    [ConcurrencyCheck]
    public DateTimeOffset ModifiedAt { get; protected set; }

    public DateTimeOffset CreatedAt => EntityIdSource.Source.CreatedAt;

    protected void AddDomainEvent(DomainEvent? e)
    {
        if (e != null)
            DomainEvents.Add(e);
    }

    internal void MarkAsCreated()
    {
        ModifiedAt = CreatedAt;
        AddDomainEvent(GetCreatedEvent());
    }

    internal void MarkAsModified()
    {
        ModifiedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(GetModifiedEvent());
    }

    internal void MarkAsDeleted() => AddDomainEvent(GetDeletedEvent());

    protected virtual EntityCreated? GetCreatedEvent() => null;
    protected virtual EntityModified? GetModifiedEvent() => null;
    protected virtual EntityDeleted? GetDeletedEvent() => null;
    
    public bool Equals(Entity? other) => ReferenceEquals(this, other) || (!IsPendingInsert && EntityIdSource == other?.EntityIdSource);
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);
    public override int GetHashCode() => EntityIdSource.GetHashCode();

    /// <summary>
    /// Returns comparison result based on Id. Null and Zero-valued ids are considered less than any other id.
    /// </summary>
    /// <returns>
    ///<li>1: if this entity's Id is greater than the other Id, which means this entity is newer than the other.</li>
    ///<li>-1: if this entity's Id is less than the other Id, which means this entity is older than the other.</li>
    ///<li>0: if they are equal, which means they are the same entity.</li>
    /// </returns>
    public int CompareTo(Entity? other)
    {
        if (Equals(other))
            return 0;
        if (other is null || other.Id == 0)
            return 1;
        if (Id == 0)
            return -1;

        return Id.CompareTo(other.Id);
    }

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
    public static bool operator >(Entity? left, Entity? right) => (left?.CompareTo(right) ?? -1) > 0;
    public static bool operator <(Entity? left, Entity? right) => (left?.CompareTo(right) ?? -1) < 0;
    public static bool operator >=(Entity? left, Entity? right) => (left?.CompareTo(right) ?? -1) >= 0;
    public static bool operator <=(Entity? left, Entity? right) => (left?.CompareTo(right) ?? -1) <= 0;
}