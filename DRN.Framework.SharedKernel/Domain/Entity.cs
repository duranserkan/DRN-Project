using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

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

public abstract class Entity(long id = 0)
{
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
            addValueFactory: id => newType,
            updateValueFactory: (id, existingType) =>
            {
                if (existingType != newType)
                    throw new InvalidOperationException($"ID {id} is used by both {existingType.FullName} and {newType.FullName}");

                return existingType; // No change needed
            });

    private List<IDomainEvent> DomainEvents { get; } = new(2);
    public IReadOnlyList<IDomainEvent> GetDomainEvents() => DomainEvents;
    public long Id { get; internal set; } = id;
    public Guid EntityId => EntityIdSource.EntityId;
    public SourceKnownEntityId EntityIdSource { get; internal set; }

    public string ExtendedProperties { get; protected set; } = null!;

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
        ModifiedAt = DateTimeOffset.UtcNow;
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

    private bool Equals(Entity other) => EntityIdSource == other.EntityIdSource;
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is Entity other && Equals(other);
    public override int GetHashCode() => EntityIdSource.GetHashCode();
    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}