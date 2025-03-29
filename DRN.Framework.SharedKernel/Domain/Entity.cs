using System.ComponentModel.DataAnnotations;
namespace DRN.Framework.SharedKernel.Domain;

public abstract class Entity
{
    private List<IDomainEvent> DomainEvents { get; } = new();
    public IReadOnlyList<IDomainEvent> GetDomainEvents() => DomainEvents;
    public long Id { get; protected set; }

    public string ExtendedProperties { get; protected set; } = null!;

    [ConcurrencyCheck]
    public DateTimeOffset ModifiedAt { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }

    protected void AddDomainEvent(DomainEvent? e)
    {
        if (e != null) DomainEvents.Add(e);
    }

    public void MarkAsCreated()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(GetCreatedEvent());
    }

    public void MarkAsModified()
    {
        ModifiedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(GetModifiedEvent());
    }

    public void MarkAsDeleted()
    {
        AddDomainEvent(GetDeletedEvent());
    }

    protected abstract EntityCreated? GetCreatedEvent();
    protected abstract EntityModified? GetModifiedEvent();
    protected abstract EntityDeleted? GetDeletedEvent();

    private bool Equals(Entity other) => Id == other.Id;
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is Entity other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}