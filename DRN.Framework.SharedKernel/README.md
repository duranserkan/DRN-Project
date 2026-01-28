[![master](https://github.com/duranserkan/DRN-Project/actions/workflows/master.yml/badge.svg?branch=master)](https://github.com/duranserkan/DRN-Project/actions/workflows/master.yml)
[![develop](https://github.com/duranserkan/DRN-Project/actions/workflows/develop.yml/badge.svg?branch=develop)](https://github.com/duranserkan/DRN-Project/actions/workflows/develop.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)

[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=bugs)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=ncloc)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=coverage)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)

# DRN.Framework.SharedKernel

> Lightweight package containing domain primitives, exceptions, and shared code suitable for Contract and Domain layers.

## TL;DR

- **Zero dependencies** - Safe for Contract/Domain layers without framework overhead
- **Domain primitives** - `SourceKnownEntity`, `AggregateRoot`, `DomainEvent` for DDD patterns
- **Type-safe exceptions** - `ExceptionFor.NotFound()`, `ExceptionFor.Validation()` map to HTTP status codes
- **JSON conventions** - Global `System.Text.Json` defaults with camelCase, enums-as-strings
- **Source Known IDs** - Internal `long` for DB performance, external `Guid` for API security

## Table of Contents

- [QuickStart: Beginner](#quickstart-beginner)
- [QuickStart: Advanced](#quickstart-advanced)
- [Domain Primitives](#domain-primitives)
- [SourceKnownRepository](#sourceknownrepository)
- [Pagination](#pagination)
- [Exceptions](#exceptions)
- [JsonConventions](#jsonconventions)
- [Attributes](#attributes)
- [AppConstants](#appconstants)

---

## QuickStart: Beginner

Define your first domain entity with automatic ID generation and domain events:

```csharp
using DRN.Framework.SharedKernel.Domain;

[EntityType(1)] // Unique byte identifier for this entity type
public class User : AggregateRoot
{
    public string Name { get; private set; }

    public User(string name)
    {
        Name = name;
        AddDomainEvent(new UserCreated(this)); // Automatically published by DrnContext
    }
}

public class UserCreated(User user) : EntityCreated(user)
{
    public string UserName => user.Name;
}
```

## QuickStart: Advanced

Complete example with pagination, validation, and exception handling:

```csharp
// Repository usage with pagination
public class UserService(ISourceKnownRepository<User> repository)
{
    public async Task<PaginationResultModel<UserDto>> GetUsersAsync(PaginationRequest request)
    {
        // Cursor-based pagination with date filtering
        var filter = EntityCreatedFilter.After(DateTimeOffset.UtcNow.AddDays(-30));
        var result = await repository.PaginateAsync(request, filter);
        
        return result.ToModel(user => new UserDto
        {
            Id = user.EntityId,  // External GUID for API
            Name = user.Name
        });
    }

    public async Task<User> GetUserAsync(Guid id)
    {
        var user = await repository.GetOrDefaultAsync(id);
        if (user is null)
            throw ExceptionFor.NotFound($"User {id} not found"); // Maps to HTTP 404
        
        return user;
    }
}
```

---

## Domain Primitives

Following definitions provide guidance and conventions for rapid and effective domain design. Their usage is optional but highly recommended for compatibility with `DrnContext` features.

### Entity and AggregateRoot

- **`SourceKnownEntity`**: The base class for all entities. Handles identity (`long Id` internal, `Guid EntityId` external), domain events, and auditing.
- **`AggregateRoot`**: Marker class for DDD aggregate roots.
- **`[EntityType(byte)]`**: Attribute defining a stable, unique byte identifier for each entity type. **Required** for ID generation and `DrnContext` startup validation.

> [!IMPORTANT]
> **Identity Rule**: Always use `Guid EntityId` (mapped as `Id` in DTOs) for all public-facing contracts, API route parameters, and external lookups. The internal `long Id` must **never** be exposed outside the infrastructure/domain boundaries.

```csharp
using DRN.Framework.SharedKernel.Domain;

[EntityType(1)] // Mandatory: Unique byte identifier for this entity type
public class User : AggregateRoot 
{
    public string Name { get; private set; }

    public User(string name) 
    {
        Name = name;
        AddDomainEvent(new UserCreated(this)); // Auto-published by DrnContext
    }
}
```

### Domain Events

Entities encapsulate their events. `DrnContext` publishes them automatically upon saving changes.

```csharp
public class UserCreated(User user) : EntityCreated(user)
{
    public string UserName => user.Name;
}

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
    public DateTimeOffset Date { get; protected init; } = DateTimeOffset.UtcNow;
}

public abstract class EntityCreated(SourceKnownEntity sourceKnownEntity) : DomainEvent(sourceKnownEntity);
public abstract class EntityModified(SourceKnownEntity sourceKnownEntity) : DomainEvent(sourceKnownEntity);
public abstract class EntityDeleted(SourceKnownEntity sourceKnownEntity) : DomainEvent(sourceKnownEntity);
```

### SourceKnownEntityId & SourceKnownId

The framework uses a Source Known identifier system (`SourceKnownEntityId`) to balance database performance (long) with external security (Guid) and type safety.

```csharp
// entityId.Valid will be true only if the GUID structure is correct AND matches the User entity type.
var entityId = user.GetEntityId(someGuid, validate: true);
```

### ID Validation & Retrieval Strategies

The framework provides three distinct ways to validate and retrieve typed identifiers based on the operational context:

**1. Injectable Utility (Recommended for Service Layer)**

Ideal for cross-cutting business logic or when you have the `sourceKnownEntityIdUtils` in scope.

```csharp
var id = sourceKnownEntityIdUtils.Validate<User>(externalGuid);
```

**2. Repository (Recommended for Data Access)**

Directly available on `ISourceKnownRepository<TEntity>`. Standardizes validation at the data entry point.

```csharp
var id = userRepository.GetEntityId(externalGuid); 
```

**3. Domain Entity (Recommended for Domain Logic)**

Uses helper methods on the `SourceKnownEntity` base class. Best for intra-domain operations.

```csharp
var id = userInstance.GetEntityId<User>(externalGuid);
```

```csharp
namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Application wide Unique Entity Type
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EntityTypeAttribute(byte entityType) : Attribute
{
    public byte EntityType { get; } = entityType;
}

public abstract class SourceKnownEntity(long id = 0) : IHasEntityId, IEquatable<SourceKnownEntity>, IComparable<SourceKnownEntity>
{
    public const int IdColumnOrder = 0;
    public const int ModifiedAtColumnOrder = 1;

    // ... Internal implementation omitted for brevity ...

    /// <summary>
    /// Internal use only, Use EntityId for external usage
    /// </summary>
    [JsonIgnore]
    [Column(Order = IdColumnOrder)]
    public long Id { get; internal set; } = id;

    /// <summary>
    /// External use only, don't use Id for external usage
    /// </summary>
    [JsonPropertyName(nameof(Id))]
    public Guid EntityId => EntityIdSource.EntityId;

    public DateTimeOffset CreatedAt => EntityIdSource.Source.CreatedAt;

    [ConcurrencyCheck]
    public DateTimeOffset ModifiedAt { get; protected internal set; }

    [JsonIgnore]
    public SourceKnownEntityId EntityIdSource { get; internal set; }
    
    // ... GetEntityId methods ...
}

public abstract class AggregateRoot(long id = 0) : SourceKnownEntity(id);

public abstract class AggregateRoot<TModel>(long id = 0) : AggregateRoot(id), IEntityWithModel<TModel> where TModel : class
{
    public TModel Model { get; set; } = null!;
}
```

```csharp
public readonly record struct SourceKnownId(
    long Id,                // The internal database ID (64-bit integer)
    DateTimeOffset CreatedAt, // Timestamp when the ID was generated
    uint InstanceId,        // Generator instance ID (for distributed uniqueness)
    byte AppId,             // Application ID
    byte AppInstanceId      // Application Instance ID
);

public readonly record struct SourceKnownEntityId(
    SourceKnownId Source,   // The decoded internal components
    Guid EntityId,          // The external opaque GUID
    byte EntityType,        // The entity type identifier (from EntityTypeAttribute)
    bool Valid              // Whether the ID structure is valid
)
{
    // Validates that this ID belongs to the specified TEntity type
    public void Validate<TEntity>() where TEntity : SourceKnownEntity 
        => Validate(SourceKnownEntity.GetEntityType<TEntity>());
        
    // Validates ID structure and checks type match
    public void Validate(byte entityType);
}
```

---

## SourceKnownRepository

`ISourceKnownRepository<TEntity>` defines a standard contract for repositories managing `AggregateRoot` entities.

*   **Standardized Access**: Provides common CRUD operations (`CreateAsync`, `GetAsync`, `DeleteAsync`).
*   **Identity Conversion**: Includes methods to convert between external `Guid`s and internal `SourceKnownEntityId`s ensuring IDs are valid.
*   **Cancellation Support**: Built-in support for `CancellationToken` propagation and merging.
*   **Streaming**: Supports `IAsyncEnumerable` for processing large datasets efficiently.

```csharp
public interface ISourceKnownRepository<TEntity> where TEntity : AggregateRoot
{
    RepositorySettings<TEntity> Settings { get; set; }
    CancellationToken CancellationToken { get; set; }
    
    // Identity Conversion & Validation
    SourceKnownEntityId GetEntityId(Guid id, bool validate = true);
    SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id) where TOtherEntity : SourceKnownEntity;
    
    // Data Access
    Task<TEntity> GetAsync(Guid id);
    Task<TEntity> GetAsync(SourceKnownEntityId id);
    Task<TEntity?> GetOrDefaultAsync(Guid id, bool validate = true);
    Task<TEntity?> GetOrDefaultAsync(SourceKnownEntityId id, bool validate = true);
    
    // Batch Retrieval
    Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids);
    Task<TEntity[]> GetAsync(IReadOnlyCollection<SourceKnownEntityId> ids);
    Task<TEntity[]> GetAllAsync(); 
    
    // Batch & Counts
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null);
    Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate);
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null);
    
    // Modification
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

> [!WARNING]
> `GetAllAsync()` returns all matching entities in a single query. This should be used **only** when the result set is guaranteed to be small or for specific maintenance tasks. Avoid in public-facing APIs.

### Filtering

`EntityCreatedFilter` provides standardized date-based filtering, useful for time-series data or audit logs.

```csharp
// Example: Get records created in the last 7 days
var filter = EntityCreatedFilter.After(DateTimeOffset.UtcNow.AddDays(-7));
var result = await repository.PaginateAsync(request, filter);
```

---

## Pagination

The framework provides a robust **cursor-based** pagination system to balance performance and usability.

### API Integration

`PaginationRequest` is natively **URL-serializable**. In Controllers, simply bind it from the query string.

```csharp
[HttpGet]
public async Task<PaginationResultModel<UserDto>> GetAsync([FromQuery] PaginationRequest request)
{
    var result = await repository.PaginateAsync(request);
    return result.ToModel(user => user.ToDto());
}
```

### Usage

```csharp
// Example usage in a repository or service
var request = PaginationRequest.DefaultWith(size: 20, direction: PageSortDirection.Descending);
var result = await repository.PaginateAsync(request);

if (result.Info.HasNext) 
{
    // Use result.Info.RequestNextPage() for the next link
}
```

---

## Exceptions

`DrnException` types map automatically to HTTP status codes via the `ExceptionFor` factory.

| Factory Method | Exception Type | HTTP Status |
|---------------|----------------|-------------|
| `ExceptionFor.Validation(msg)` | `ValidationException` | **400** |
| `ExceptionFor.Unauthorized(msg)` | `UnauthorizedException` | **401** |
| `ExceptionFor.Forbidden(msg)` | `ForbiddenException` | **403** |
| `ExceptionFor.NotFound(msg)` | `NotFoundException` | **404** |
| `ExceptionFor.Conflict(msg)` | `ConflictException` | **409** |
| `ExceptionFor.Expired(msg)` | `ExpiredException` | **410** |
| `ExceptionFor.UnprocessableEntity(msg)` | `UnprocessableEntityException` | **422** |
| `ExceptionFor.Configuration(msg)` | `ConfigurationException` | **500** |
| `ExceptionFor.MaliciousRequest(msg)` | `MaliciousRequestException` | **Abort** |

```csharp
namespace DRN.Framework.SharedKernel;

/// <summary>
/// DrnExceptions are handled by scope handler and can be used to short circuit the processing pipeline
/// </summary>
public abstract class DrnException(string message, Exception? ex, string? category, short? status = null)
    : Exception(message, ex)
{
    public const string DefaultCategory = "default";
    public string Category { get; } = category ?? DefaultCategory;
    public short Status { get; } = status ?? 500;
}

public static class ExceptionFor
{
    private const string Default = DrnException.DefaultCategory;

    public static ValidationException Validation(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);
        
    public static NotFoundException NotFound(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);
        
    // ... other factory methods ...
}
```

---

## JsonConventions

`JsonConventions` globally overrides `System.Text.Json` settings. These are automatically applied by `DrnTestContext` (in tests) and `DrnHostBuilder` (in hosted apps).

**Applied Settings:**
- `JsonSerializerDefaults.Web` (CamelCase naming, case-insensitive)
- `JsonStringEnumConverter` (Enums as strings)
- `AllowTrailingCommas` = true
- `NumberHandling` = AllowReadingFromString

```csharp
namespace DRN.Framework.SharedKernel.Json;

public static class JsonConventions
{
    static JsonConventions()
    {
        UpdateDefaultJsonSerializerOptions();
    }

    public static readonly JsonSerializerOptions DefaultOptions = SetJsonDefaults();

    public static JsonSerializerOptions SetJsonDefaults(JsonSerializerOptions? options = null)
    {
        options ??= new JsonSerializerOptions(JsonSerializerDefaults.Web);

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new Int64ToStringConverter());
        options.Converters.Add(new Int64NullableToStringConverter());
        options.AllowTrailingCommas = true;
        options.PropertyNameCaseInsensitive = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        options.MaxDepth = 32;
        // ...
        return options;
    }
}
```

---

## Attributes

### `[IgnoreLog]`
Excludes sensitive properties or entire classes from scoped logging.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
public class IgnoreLogAttribute : Attribute;
```

### `[SecureKey]`
Validates that a string meets secure key requirements (length, character classes).

---

## AppConstants

```csharp
namespace DRN.Framework.SharedKernel;

public static class AppConstants
{
    public static int ProcessId { get; } = Environment.ProcessId;
    public static Guid AppInstanceId { get; } = Guid.NewGuid();
    public static string EntryAssemblyName { get; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "Entry Assembly Not Found";
    public static string TempPath { get; } = GetTempPath(); //Cleans directory at every startup
    public static string LocalIpAddress { get; } = GetLocalIpAddress();
}
```

---
**Semper Progressivus: Always Progressive**