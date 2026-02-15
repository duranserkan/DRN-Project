---
name: drn-sharedkernel
description: DRN.Framework.SharedKernel - Foundational domain primitives (Entity, AggregateRoot, DomainEvent), exception hierarchy, repository contracts, pagination, and JSON conventions. Essential for domain modeling, entity design, and repository implementation. Keywords: entity, aggregate-root, domain-event, repository, pagination, exception, json, domain-modeling, source-known-id, entity-type
last-updated: 2026-02-15
difficulty: intermediate
---

# DRN.Framework.SharedKernel

> Lightweight domain primitives, exceptions, and shared code. No external DRN dependencies — safe for Contract and Domain layers.

## When to Apply
- Defining domain entities and aggregates
- Working with domain events
- Using or extending DRN exceptions
- Understanding repository contracts
- Accessing JSON serialization conventions

---

## DiSCOS Alignment

| DiSCOS Principle | SharedKernel Expression |
|------------------|------------------------|
| Security First | Source-Known IDs hide internal `long` behind external `Guid`; `MaliciousRequestException` aborts connection |
| Abstraction | Domain primitives (`SourceKnownEntity`, `AggregateRoot`, `DomainEvent`) encode patterns; implement specifics |
| TRIZ (Separation in Space) | Dual-ID system resolves DB performance vs. external security without tradeoff |

---

## Entity Base Class

```csharp
public abstract class SourceKnownEntity(long id = 0)
{
    public long Id { get; internal set; }
    [ConcurrencyCheck]
    public DateTimeOffset ModifiedAt { get; protected internal set; }
    public DateTimeOffset CreatedAt { get; }
    public SourceKnownEntityId EntityIdSource { get; internal set; }
    public Guid EntityId => EntityIdSource.EntityId;

    // ID validation
    public SourceKnownEntityId GetEntityId(Guid id, byte entityType);
    public SourceKnownEntityId GetEntityId<TEntity>(Guid id);
    
    // Auto-called by DrnContext
    internal void MarkAsCreated();   // Sets CreatedAt, adds created event
    internal void MarkAsModified();  // Sets ModifiedAt, adds modified event
    internal void MarkAsDeleted();   // Adds deleted event
}
```

> [!WARNING]
> Each entity requires unique `[EntityType(byte)]`. DrnContext validates at startup.

### AggregateRoot

```csharp
public abstract class AggregateRoot(long id = 0) : SourceKnownEntity(id);
public abstract class AggregateRoot<TModel>(long id = 0) : AggregateRoot(id), IEntityWithModel<TModel>
{
    public TModel Model { get; set; } = null!;  // Auto-mapped to jsonb
}
```

### Domain Events

```csharp
public interface IDomainEvent { Guid Id { get; } DateTimeOffset Date { get; } Guid EntityId { get; } }
public abstract class DomainEvent(SourceKnownEntity entity) : IDomainEvent;
public abstract class EntityCreated(SourceKnownEntity entity) : DomainEvent(entity);
public abstract class EntityModified(SourceKnownEntity entity) : DomainEvent(entity);
public abstract class EntityDeleted(SourceKnownEntity entity) : DomainEvent(entity);
```

---

## Source-Known Identity System

Balances DB performance (`long`) with external security (`Guid`) and type safety.

```csharp
public readonly record struct SourceKnownId(
    long Id, DateTimeOffset CreatedAt, uint InstanceId, byte AppId, byte AppInstanceId);

public readonly record struct SourceKnownEntityId(
    SourceKnownId Source, Guid EntityId, byte EntityType, bool Valid);
```

### Validation Approaches

**1. Injectable Utility** (service layer — recommended):
```csharp
var id = sourceKnownEntityIdUtils.Validate<User>(externalGuid);
```

**2. Repository** (data entry point):
```csharp
var id = userRepository.GetEntityId(externalGuid);
```

**3. Domain Entity** (intra-domain):
```csharp
var id = userInstance.GetEntityId<User>(externalGuid);
```

---

## Repository Contract

```csharp
public interface ISourceKnownRepository<TEntity> where TEntity : AggregateRoot
{
    RepositorySettings<TEntity> Settings { get; set; }
    CancellationToken CancellationToken { get; set; }
    
    // Identity Conversion & Validation
    SourceKnownEntityId GetEntityId(Guid id, bool validate = true);
    SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id) where TOtherEntity : SourceKnownEntity;
    
    // Retrieval by GUID (External ID)
    Task<TEntity> GetAsync(Guid id);
    Task<TEntity> GetAsync(SourceKnownEntityId id);
    Task<TEntity?> GetOrDefaultAsync(Guid id, bool validate = true);
    Task<TEntity?> GetOrDefaultAsync(SourceKnownEntityId id, bool validate = true);
    
    // Batch Retrieval
    Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids);
    Task<TEntity[]> GetAsync(IReadOnlyCollection<SourceKnownEntityId> ids);
    Task<TEntity[]> GetAllAsync(); // ⚠️ Use with caution - only for small/known-size collections
    
    // Queries
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null);
    Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate);
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null);
    
    // Commands
    void Add(params IReadOnlyCollection<TEntity> entities);
    void Remove(params IReadOnlyCollection<TEntity> entities);
    Task<int> CreateAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<Guid> ids);
    Task<int> DeleteAsync(params IReadOnlyCollection<SourceKnownEntityId> ids);
    Task<int> SaveChangesAsync();
    
    // Pagination
    Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
    Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationResultInfo? resultInfo = null, long jumpTo = 1,
        int pageSize = -1, int maxSize = -1, PageSortDirection direction = PageSortDirection.None,
        long totalCount = -1, bool updateTotalCount = false);
    IAsyncEnumerable<PaginationResultModel<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
}
```

**Key Behaviors**:
- `Get(OrDefault)Async` with `Guid` auto-validates ID format and EntityType byte before querying
- `PaginateAllAsync` returns `IAsyncEnumerable` for efficient large dataset streaming
- `CancellationToken` supports merging via `MergeCancellationTokens`

### Pagination

```csharp
public class PaginationRequest
{
    public long PageNumber { get; init; }
    public PageSize PageSize { get; init; }
    public PageCursor PageCursor { get; init; } // Holds FirstId/LastId for stable paging
    public static PaginationRequest Default;
}

public class PaginationResultModel<TModel>
{
    public IReadOnlyList<TModel> Items { get; }
    public PaginationResultInfo Info { get; } // Next/Prev page metadata
    public PaginationResultModel<TMapped> ToModel<TMapped>(Func<TModel, TMapped> mapper);
}
```

- **Stable Navigation**: `PageCursor` with `FirstId`/`LastId` prevents data inconsistencies during concurrent viewing
- **Bi-directional**: `RequestNextPage()`, `RequestPreviousPage()`, `RequestPage(n)` on `PaginationResultInfo`
- **URL-Serializable**: `PaginationRequest` natively works with `[FromQuery]`
- **Filtering**: `EntityCreatedFilter.After(date)` for date-based filtering

```csharp
// API pagination pattern
var filter = EntityCreatedFilter.After(DateTimeOffset.UtcNow.AddDays(-7));
var result = await repository.PaginateAsync(request, filter);
return result.ToModel(entity => entity.ToDto());
```

### DTO Rules

> [!IMPORTANT]
> 1. DTOs **must** derive from `Dto` base class (primary constructor accepting `SourceKnownEntity?`)
> 2. Public APIs must **never** return Entities — always DTOs
> 3. Expose only `Guid` IDs, never `long Id` or `SourceKnownEntityId`

---

## Exceptions

| Factory | Exception | HTTP Status |
|---------|-----------|-------------|
| `ExceptionFor.Validation(msg)` | `ValidationException` | 400 |
| `ExceptionFor.Unauthorized(msg)` | `UnauthorizedException` | 401 |
| `ExceptionFor.Forbidden(msg)` | `ForbiddenException` | 403 |
| `ExceptionFor.NotFound(msg)` | `NotFoundException` | 404 |
| `ExceptionFor.Conflict(msg)` | `ConflictException` | 409 |
| `ExceptionFor.Expired(msg)` | `ExpiredException` | 410 |
| `ExceptionFor.UnprocessableEntity(msg)` | `UnprocessableEntityException` | 422 |
| `ExceptionFor.Configuration(msg)` | `ConfigurationException` | 500 |
| `ExceptionFor.MaliciousRequest(msg)` | `MaliciousRequestException` | Abort |

All factories accept optional `category` parameter for sub-classification: `ExceptionFor.NotFound("User not found", category: "Users")`. Exceptions expose `Category` and `Status` properties.

---

## JSON Conventions

`JsonConventions.DefaultOptions` — globally applied: Web defaults, enum→string, long→string, camelCase, AllowTrailingCommas, MaxDepth=32. Auto-applied by `DrnTestContext` and `DrnProgramBase`.

---

## AppConstants

```csharp
AppConstants.ProcessId;          AppConstants.AppInstanceId;
AppConstants.EntryAssemblyName;  AppConstants.TempPath;  // Cleaned at startup
AppConstants.LocalIpAddress;
```

## Attributes

| Attribute | Purpose |
|-----------|---------|
| `[IgnoreLog]` | Exclude properties from scoped logging |
| `[SecureKey]` | Validate string meets secure key requirements |
| `[EntityType(byte)]` | Unique entity type ID for Source-Known IDs |

---

## Related Skills

- [drn-domain-design.md](../drn-domain-design/SKILL.md) - Domain & Repository patterns
- [drn-entityframework.md](../drn-entityframework/SKILL.md) - DrnContext usage
- [drn-utils.md](../drn-utils/SKILL.md) - DI and settings
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Domain modeling

---

## Global Usings

```csharp
global using DRN.Framework.SharedKernel.Domain;
global using DRN.Framework.SharedKernel;
global using DRN.Framework.SharedKernel.Json;
```