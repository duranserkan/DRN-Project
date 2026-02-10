---
name: drn-sharedkernel
description: DRN.Framework.SharedKernel - Foundational domain primitives (Entity, AggregateRoot, DomainEvent), exception hierarchy, repository contracts, pagination, and JSON conventions. Essential for domain modeling, entity design, and repository implementation. Keywords: entity, aggregate-root, domain-event, repository, pagination, exception, json, domain-modeling, ddd, source-known-id, skills, drn, domain, design, overview, drn, framework, utils, entity, framework, overview, ddd, architecture
---

# DRN.Framework.SharedKernel

> Lightweight package containing domain primitives, exceptions, and shared code suitable for Contract and Domain layers.

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

> [!WARNING]
> Each entity requires unique `[EntityType(byte)]` attribute. DrnContext validates at startup and throws `ConfigurationException` on duplicates.
> 
> [!IMPORTANT]
> **DTO Mapping & Exposure Rules**:
> 1. **Inheritance**: All DTOs **must derive from the `Dto` base class**.
> 2. **Primary Constructor**: DTOs must use a primary constructor accepting `SourceKnownEntity?` to auto-map `Id`, `CreatedAt`, and `ModifiedAt`.
> 3. **No Entities in API**: Public APIs must never return or accept Entities. Always use DTOs/Response Models.
> 4. **Guid IDs**: Public APIs must only expose `Guid` identifiers. DTOs should not include SourceKnownEntityIds or SourceKnownIds.
> 5. **Consice**: DTOs should be concise and only include necessary properties. DTOs should not include pagination results etc.

### AggregateRoot

```csharp
public abstract class AggregateRoot(long id = 0) : SourceKnownEntity(id);

public abstract class AggregateRoot<TModel>(long id = 0) : AggregateRoot(id), IEntityWithModel<TModel> where TModel : class
{
    public TModel Model { get; set; } = null!;
}
```

- `AggregateRoot`: Marker class for DDD aggregate roots.
- `AggregateRoot<TModel>`: Aggregate root with a JSON model property automatically mapped to `jsonb` by `DrnContext`.

### Domain Events

```csharp
public interface IDomainEvent
{
    Guid Id { get; }
    DateTimeOffset Date { get; }
    Guid EntityId { get; }
}

public abstract class DomainEvent(SourceKnownEntity entity) : IDomainEvent;
public abstract class EntityCreated(SourceKnownEntity entity) : DomainEvent(entity);
public abstract class EntityModified(SourceKnownEntity entity) : DomainEvent(entity);
public abstract class EntityDeleted(SourceKnownEntity entity) : DomainEvent(entity);
```

### SourceKnownEntityId & SourceKnownId

The framework uses a Source Known identifier system to balance DB performance (`long`) with external security (`Guid`) and type safety.

```csharp
public readonly record struct SourceKnownId(
    long Id,                  // Internal DB ID (primary key)
    DateTimeOffset CreatedAt, // Timestamp of generation
    uint InstanceId,          // Generator instance ID
    byte AppId,               // Application ID
    byte AppInstanceId        // Application Instance ID
);

public readonly record struct SourceKnownEntityId(
    SourceKnownId Source,   // Decoded internal components
    Guid EntityId,          // Opaque external GUID
    byte EntityType,        // Entity type discriminator
    bool Valid              // Structural validity flag
);
```

### ID Validation & Retrieval Strategies

Three approaches for validating and retrieving typed identifiers:

**1. Injectable Utility** (Recommended for service layer):
```csharp
var id = sourceKnownEntityIdUtils.Validate<User>(externalGuid);
```

**2. Repository** (At data entry point):
```csharp
var id = userRepository.GetEntityId(externalGuid);
```

**3. Domain Entity** (Intra-domain operations):
```csharp
var id = userInstance.GetEntityId<User>(externalGuid);
```

### SourceKnownRepository

`ISourceKnownRepository<TEntity>` provides a standard interface for retrieving and managing source-known entities.

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
    Task<TEntity[]> GetAllAsync(); // ⚠️ Use with caution - only for small/known result sets
    
    // Batch & Counts
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
    IAsyncEnumerable<PaginationResultModel<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
}
```

**Key Behaviors**:
- **Validation**: `Get(OrDefault)Async` methods with `Guid` automatically validate the ID format and EntityType byte before querying the database.
- **SourceKnownEntityId**: Wraps the GUID and validation logic.
- **Cancellation**: Supports `CancellationToken` via property or `MergeCancellationTokens`.
- **Streaming**: `PaginateAllAsync` returns `IAsyncEnumerable` for efficient large dataset processing.

### Filtering

`EntityCreatedFilter` provides standardized date-based filtering:

```csharp
var filter = EntityCreatedFilter.After(DateTimeOffset.UtcNow.AddDays(-7));
var result = await repository.PaginateAsync(request, filter);
```

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
- **URL-Serializable**: `PaginationRequest` is natively URL-serializable. In Controllers, bind from query string with `[FromQuery]`.

**API Integration**:
```csharp
[HttpGet]
public async Task<PaginationResultModel<UserDto>> GetAsync([FromQuery] PaginationRequest request)
{
    var result = await repository.PaginateAsync(request);
    return result.ToModel(user => user.ToDto());
}
```

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
// - Int64ToStringConverter / Int64NullableToStringConverter
// - AllowTrailingCommas = true
// - PropertyNameCaseInsensitive = true
// - PropertyNamingPolicy = CamelCase
// - NumberHandling = AllowReadingFromString
// - MaxDepth = 32
```

**Applied automatically by**:
- `DrnTestContext` in tests
- `DrnProgramBase` in hosted apps

---

## AppConstants

Static application-wide constants:

```csharp
AppConstants.ProcessId;          // Current process ID
AppConstants.AppInstanceId;      // Unique Guid per app instance
AppConstants.EntryAssemblyName;  // Entry assembly name
AppConstants.TempPath;           // Cleaned temp directory at every startup
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

### SecureKeyAttribute

Validates that a string meets secure key requirements (length, character classes).

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

## Global Usings

```csharp
global using DRN.Framework.SharedKernel.Domain;
global using DRN.Framework.SharedKernel;
global using DRN.Framework.SharedKernel.Json;
```

---

## Related Skills

- [drn-domain-design.md](../drn-domain-design/SKILL.md) - Domain & Repository patterns
- [overview-drn-framework.md](../overview-drn-framework/SKILL.md) - Framework architecture
- [drn-utils.md](../drn-utils/SKILL.md) - DI and settings
- [drn-entityframework.md](../drn-entityframework/SKILL.md) - DrnContext usage
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Domain modeling

---
