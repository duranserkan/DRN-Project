namespace DRN.Framework.SharedKernel.Domain;

public interface IDomainEvent
{
    Guid Id { get; }
    DateTimeOffset Date { get; }
    Guid EntityId { get; }
}

public abstract class DomainEvent(Entity entity) : IDomainEvent
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
    public Guid EntityId => entity.EntityId;
    public DateTimeOffset Date { get; protected init; } = DateTimeOffset.UtcNow;
}

public abstract class EntityCreated(Entity entity) : DomainEvent(entity);

public abstract class EntityModified(Entity entity) : DomainEvent(entity);

public abstract class EntityDeleted(Entity entity) : DomainEvent(entity);