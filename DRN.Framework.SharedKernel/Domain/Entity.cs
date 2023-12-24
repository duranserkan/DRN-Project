namespace DRN.Framework.SharedKernel.Domain;

public abstract class Entity
{
    protected List<DomainEvent> DomainEvents { get; } = new();

    public long Id { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset ModifiedAt { get; protected set; }

    public virtual void MarkAsCreated()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTimeOffset.UtcNow;
        var created = GetCreatedEvent();
        AddDomainEvent(created);
    }

    public virtual void MarkAsModified()
    {
        ModifiedAt = DateTimeOffset.UtcNow;
        var modified = GetModifiedEvent();
        AddDomainEvent(modified);
    }

    public virtual void MarkAsDeleted()
    {
        var deleted = GetDeletedEvent();
        AddDomainEvent(deleted);
    }

    public virtual EntityCreated? GetCreatedEvent() => null;

    public virtual EntityModified? GetModifiedEvent() => null;

    public virtual EntityDeleted? GetDeletedEvent() => null;

    protected void AddDomainEvent(DomainEvent? e)
    {
        if (e != null) DomainEvents.Add(e);
    }

    protected bool Equals(Entity other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is Entity other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(Entity? left, Entity? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Entity? left, Entity? right)
    {
        return !Equals(left, right);
    }
}