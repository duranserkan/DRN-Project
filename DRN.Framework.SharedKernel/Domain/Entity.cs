using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Application wide Unique Entity Id
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EntityTypeIdAttribute(ushort id) : Attribute
{
    /// <summary>
    /// Application wide Unique Entity Type Id
    /// </summary>
    public ushort Id { get; } = id;
}

public abstract class Entity
{
    private static readonly ConcurrentDictionary<Type, ushort> TypeToIdMap = new();
    private static readonly ConcurrentDictionary<ushort, Type> IdToTypeMap = new();
    //Todo: Scan assemblies at startup to detect conflicts proactively.
    public static ushort GetEntityTypeId<TEntity>() where TEntity : Entity => GetEntityTypeId(typeof(TEntity));
    public static ushort GetEntityTypeId<TEntity>(TEntity entity) where TEntity : Entity => GetEntityTypeId(entity.GetType());

    private static ushort GetEntityTypeId(Type entityType) =>
        TypeToIdMap.GetOrAdd(entityType, type =>
        {
            var attribute = type.GetCustomAttribute<EntityTypeIdAttribute>();
            if (attribute == null)
                throw new InvalidOperationException($"{type.Name} must use {nameof(EntityTypeIdAttribute)}");

            EnsureUniqueId(type, attribute.Id);
            return attribute.Id;
        });

    private static void EnsureUniqueId(Type newType, ushort newId) =>
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
    public long Id { get; internal set; }
    public Guid EntityId => EntityIdInfo.EntityId;
    public EntityIdInfo EntityIdInfo { get; internal set; }
    public string ExtendedProperties { get; protected set; } = null!;
    [ConcurrencyCheck] public DateTimeOffset ModifiedAt { get; protected set; }

    protected void AddDomainEvent(DomainEvent? e)
    {
        if (e != null)
            DomainEvents.Add(e);
    }

    internal void MarkAsCreated(long id)
    {
        Id = id;
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

    private bool Equals(Entity other) => EntityIdInfo == other.EntityIdInfo;
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is Entity other && Equals(other);
    public override int GetHashCode() => EntityIdInfo.GetHashCode();
    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}