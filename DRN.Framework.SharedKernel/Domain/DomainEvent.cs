using DRN.Framework.SharedKernel.Utils;

namespace DRN.Framework.SharedKernel.Domain;

public interface IDomainEvent
{
    Guid Id { get; }
    DateTimeOffset Date { get; }
    Guid EntityId { get; }
}

public abstract class DomainEvent(SourceKnownEntity sourceKnownEntity) : IDomainEvent
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
    public Guid EntityId => sourceKnownEntity.EntityId;
    public DateTimeOffset Date { get; protected init; } = DateTimeProvider.UtcNow;
}

public abstract class EntityCreated(SourceKnownEntity sourceKnownEntity) : DomainEvent(sourceKnownEntity);

public abstract class EntityModified(SourceKnownEntity sourceKnownEntity) : DomainEvent(sourceKnownEntity);

public abstract class EntityDeleted(SourceKnownEntity sourceKnownEntity) : DomainEvent(sourceKnownEntity);