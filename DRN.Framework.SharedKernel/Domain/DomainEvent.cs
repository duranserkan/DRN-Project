namespace DRN.Framework.SharedKernel.Domain;

public interface IDomainEvent
{
    Guid Id { get; }
    DateTimeOffset Date { get; }
    long EntityId { get; }
    string EntityName { get; }
}

public abstract class DomainEvent(Entity entity) : IDomainEvent
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
    public DateTimeOffset Date { get; protected init; } = DateTimeOffset.UtcNow;
    public long EntityId => entity.Id;
    public string EntityName { get; protected init; } = entity.GetType().FullName!;
}

public abstract class EntityCreated(Entity entity) : DomainEvent(entity);

public abstract class EntityModified(Entity entity) : DomainEvent(entity);

public abstract class EntityDeleted(Entity entity) : DomainEvent(entity);