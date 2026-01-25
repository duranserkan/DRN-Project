---
name: drn-sharedkernel
description: DRN.Framework.SharedKernel - Foundational domain primitives (Entity, AggregateRoot, DomainEvent), exception hierarchy, repository contracts, pagination, and JSON conventions. Essential for domain modeling, entity design, and repository implementation. Keywords: entity, aggregate-root, domain-event, repository, pagination, exception, json, domain-modeling, ddd, source-known-id, skills, drn domain design, overview drn framework, drn utils, drn entity framework, overview ddd architecture
---

# DRN.Framework.SharedKernel

> Lightweight package containing domain primitives and shared code suitable for Contract and Domain layers.

## When to Apply
- Defining domain entities and aggregates
- Working with domain events
- Using or extending DRN exceptions
- Understanding JSON serialization conventions
- Accessing application constants

---

## Package Purpose

SharedKernel is the **lightest** DRN package with **no external DRN dependencies**. It can be safely referenced by:
- Contract/API layers (DTOs)
- Domain layers (Entities)
- Any project needing DRN primitives

---

## Directory Structure

```
DRN.Framework.SharedKernel/
├── Domain/           # Entity, AggregateRoot, DomainEvent, IDomainEvent, EntityTypeAttribute
├── Attributes/       # IgnoreLog, SecureKeyAttribute
├── Enums/            # AppEnvironment
├── Json/             # JsonConventions
├── AppConstants.cs   # Global application constants
└── Exceptions.cs     # DrnException hierarchy
```

---

## Domain Primitives

### Entity Base Class

All entities inherit from `SourceKnownEntity`:

```csharp
public abstract class SourceKnownEntity(long id = 0) : IHasEntityId, IEquatable<SourceKnownEntity>, IComparable<SourceKnownEntity>
{
    public long Id { get; internal set; }
    
    [ConcurrencyCheck]
    public DateTimeOffset ModifiedAt { get; protected internal set; }
    public DateTimeOffset CreatedAt { get; }
    
    public SourceKnownEntityId EntityIdSource { get; internal set; }
    public Guid EntityId => EntityIdSource.EntityId;

    // ID Generation & Validation
    public SourceKnownEntityId GetEntityId(Guid id, byte entityType);
    public SourceKnownEntityId GetEntityId(Guid id, bool validate = true); // Validates ID format and type
    public SourceKnownEntityId GetEntityId<TEntity>(Guid id) where TEntity : SourceKnownEntity;
    
    internal void MarkAsCreated();
    internal void MarkAsModified();
    internal void MarkAsDeleted();
    
    protected virtual EntityCreated? GetCreatedEvent();
    protected virtual EntityModified? GetModifiedEvent();
    protected virtual EntityDeleted? GetDeletedEvent();
}
```

**Key Features**:
- Long ID (internal) and Guid EntityId (external)
- DateTimeOffset timestamps with optimistic concurrency
- Encapsulated domain events
- Automatic status marking by DrnContext

### AggregateRoot

```csharp
public abstract class AggregateRoot : SourceKnownEntity;
```

Marker class for DDD aggregate roots.

### Domain Events

```csharp
public interface IDomainEvent
{
    Guid Id { get; }
    DateTimeOffset Date { get; }
    long Next<TEntity>() where TEntity : SourceKnownEntity;
    string EntityName { get; }
}

public abstract class DomainEvent(SourceKnownEntity entity) : IDomainEvent;
public abstract class EntityCreated(SourceKnownEntity entity) : DomainEvent(entity);
public abstract class EntityModified(SourceKnownEntity entity) : DomainEvent(entity);
public abstract class EntityDeleted(SourceKnownEntity entity) : DomainEvent(entity);
```

### SourceKnownRepository

`ISourceKnownRepository<TEntity>` provides a standard interface for retrieving and managing source-known entities.

```csharp
public interface ISourceKnownRepository<TEntity> where TEntity : AggregateRoot
{
    // Retrieval by GUID (External ID)
    Task<TEntity> GetAsync(Guid id);
    Task<TEntity> GetAsync(SourceKnownEntityId id);
    Task<TEntity?> GetOrDefaultAsync(Guid id, bool validate = true); // Validates ID before query
    
    // Batch Retrieval
    Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids);
    Task<TEntity[]> GetAllAsync(); // ⚠️ Use with caution
    
    // CRUD
    void Add(params IReadOnlyCollection<TEntity> entities);
    void Remove(params IReadOnlyCollection<TEntity> entities);
    Task<int> SaveChangesAsync();
    
    // Utility
    SourceKnownEntityId GetEntityId(Guid id, bool validate = true);
    SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id);

    // Pagination
    Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
    IAsyncEnumerable<PaginationResultModel<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
}
```

**Key Behaviors**:
- **Validation**: `Get(OrDefault)Async` methods with `Guid` automatically validate the ID format and EntityType byte before querying the database.
- **SourceKnownEntityId**: Wraps the GUID and validation logic.
- **Cancellation**: Supports `CancellationToken` via property or `MergeCancellationTokens`.

### Pagination

The framework uses a cursor-based pagination system that supports both offset and cursor navigation.

```csharp
public class PaginationRequest
{
    public long PageNumber { get; init; }
    public PageSize PageSize { get; init; }
    public PageCursor PageCursor { get; init; } // Holds FirstId/LastId for stable paging
    
    // Factories
    public static PaginationRequest Default;
    public static PaginationRequest From(PaginationResultInfo? info, long jumpTo, ...);
}

public class PaginationResultModel<TModel>
{
    public IReadOnlyList<TModel> Items { get; }
    public PaginationResultInfo Info { get; } // Next/Prev page metadata
}
```

**Key Features**:
- **Stable Navigation**: Uses `PageCursor` with `FirstId` and `LastId` to prevent data inconsistencies when items are added/deleted during concurrent viewing.
- **Bi-directional**: Supports `Next`, `Previous`, and `Refresh` navigation.
- **Infinite Scroll**: `PaginateAllAsync` allows streaming valid pages sequentially.

---

## Exceptions

DRN exceptions map to HTTP status codes via `ExceptionFor` factory:

| Factory Method | Exception | Status |
|---------------|-----------|--------|
| `ExceptionFor.Validation(msg)` | `ValidationException` | 400 |
| `ExceptionFor.Unauthorized(msg)` | `UnauthorizedException` | 401 |
| `ExceptionFor.Forbidden(msg)` | `ForbiddenException` | 403 |
| `ExceptionFor.NotFound(msg)` | `NotFoundException` | 404 |
| `ExceptionFor.Conflict(msg)` | `ConflictException` | 409 |
| `ExceptionFor.Expired(msg)` | `ExpiredException` | 410 |
| `ExceptionFor.UnprocessableEntity(msg)` | `UnprocessableEntityException` | 422 |
| `ExceptionFor.Configuration(msg)` | `ConfigurationException` | 500 |
| `ExceptionFor.MaliciousRequest(msg)` | `MaliciousRequestException` | Abort |

```csharp
// Usage
throw ExceptionFor.NotFound("User not found", category: "Users");
```

**Exception Properties**:
- `Category` - Subcategory for classification
- `Status` - HTTP status code

---

## JSON Conventions

`JsonConventions` globally overrides `System.Text.Json`:

```csharp
JsonConventions.DefaultOptions; // Access global options

// Applied settings:
// - JsonSerializerDefaults.Web
// - JsonStringEnumConverter
// - AllowTrailingCommas = true
// - PropertyNameCaseInsensitive = true
// - PropertyNamingPolicy = CamelCase
// - NumberHandling = AllowReadingFromString
```

**Applied automatically by**:
- `DrnTestContext` in tests
- `DrnProgramBase` in hosted apps

---

## AppConstants

Static application-wide constants:

```csharp
AppConstants.ProcessId;          // Current process ID
AppConstants.ApplicationId;      // Unique Guid per app instance
AppConstants.EntryAssemblyName;  // Entry assembly name
AppConstants.TempPath;           // Cleaned temp directory
AppConstants.LocalIpAddress;     // Machine IP address
```

---

## Attributes

### IgnoreLogAttribute

Excludes properties/objects from scoped logging:

```csharp
[IgnoreLog]
public class SensitiveData { }

public class User
{
    public string Name { get; set; }
    
    [IgnoreLog]
    public string Password { get; set; }
}
```

### EntityTypeAttribute

Marks entities with unique type ID for Source-Known IDs:

```csharp
[EntityType(1)]
public class User : SourceKnownEntity { }

[EntityType(2)]
public class Order : SourceKnownEntity { }
```

> See: [drn-entityframework.md](../drn-entityframework/SKILL.md)

---

## Related Skills

- [drn-domain-design.md](../drn-domain-design/SKILL.md) - Domain & Repository patterns
- [overview-drn-framework.md](../overview-drn-framework/SKILL.md) - Framework architecture
- [drn-utils.md](../drn-utils/SKILL.md) - DI and settings
- [drn-entityframework.md](../drn-entityframework/SKILL.md) - DrnContext usage
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Domain modeling

---
