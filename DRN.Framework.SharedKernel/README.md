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
- **Shared extensions** - Casing and safe path helpers for lower-layer packages
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
- [Shared Extensions](#shared-extensions)
- [AppConstants](#appconstants)
- [Global Usings](#global-usings)
- [Related Packages](#related-packages)

---

## QuickStart: Beginner

Define your first domain entity with automatic ID generation and collected domain events:

```csharp
using DRN.Framework.SharedKernel.Domain;

[EntityType(1)] // Unique byte identifier for this entity type
public class User : AggregateRoot
{
    public string Name { get; private set; }

    public User(string name)
    {
        Name = name;
        AddDomainEvent(new UserCreated(this)); // Collected on the entity for infrastructure handling
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
        
        return result.ToModel(user => new UserDto(user)
        {
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

Core primitives provide conventions for rapid and effective domain design and ensure compatibility with `DrnContext` features.

### Entity and AggregateRoot

- **`SourceKnownEntity`**: The base class for all entities. Handles identity (`long Id` internal, `Guid EntityId` external), domain events, and auditing.
- **`AggregateRoot`**: Marker class for DDD aggregate roots.
- **`[EntityType(byte)]`**: Attribute defining a stable, unique byte identifier for each entity type. **Required** for ID generation and `DrnContext` startup validation.

> [!IMPORTANT]
> **Identity Rule**: Always use `Guid EntityId` (mapped as `Id` in DTOs) for all public-facing contracts, API route parameters, and external lookups. The internal `long Id` must **never** be exposed outside the infrastructure/domain boundaries.
>
> **DTO Mapping Rule**: DTOs should implement a primary constructor accepting a `SourceKnownEntity?` to automatically map `Id`, `CreatedAt`, and `ModifiedAt`. Avoid manual mapping of these fields.
>
> **Entity Exposure Prohibition**: Entities must never be exposed via public APIs. Always map to DTOs or Response Models. Entities are only permitted in Razor Pages (Internal UI).

```csharp
using DRN.Framework.SharedKernel.Domain;

[EntityType(1)] // Mandatory: Unique byte identifier for this entity type
public class User : AggregateRoot 
{
    public string Name { get; private set; }

    public User(string name) 
    {
        Name = name;
        AddDomainEvent(new UserCreated(this)); // Collected on the entity for infrastructure handling
    }
}
```

### Domain Events

Entities encapsulate domain events through `AddDomainEvent` / `GetDomainEvents`. SharedKernel defines the event model and entity collection behavior; publication/outbox dispatch is handled by infrastructure when implemented.

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

The framework uses a three-tier Source Known identifier system (`SKID`, `SKEID`, and `Secure SKEID`) to balance database performance (long) with internal routing validation and external security (Guid) and type safety.

- **Source Known ID (SKID)**: Core 64-bit integer acting as the database primary key. Extremely fast, sortable, and compact (8 bytes).
- **Source Known Entity ID (SKEID)**: Internal 128-bit UUID-compatible value extending SKID with an entity type, epoch byte, and BLAKE3 keyed MAC for zero-lookup validation.
- **Secure Source Known Entity ID (Secure SKEID)**: External 128-bit value encrypting the entire SKEID via single-block AES-256-ECB. Indistinguishable from random bytes.

`ISourceKnownEntityIdOperations` defines the core contract for entity ID operations (`Generate`, `Parse`, `ToSecure`, `ToPlain`). It lives in SharedKernel so domain entities can use it without referencing Utils. `ISourceKnownEntityIdUtils` in Utils inherits this interface and provides the full implementation, which EF interceptors inject into entities automatically.

```csharp
// Validates GUID structure and the User entity type.
var entityId = user.GetEntityId<User>(someGuid);
```

### ID Validation & Retrieval Strategies

The framework provides three distinct ways to validate and retrieve typed identifiers based on the operational context:

#### 1. Injectable Utility

Recommended for service layer logic or when `sourceKnownEntityIdUtils` is in scope.

```csharp
var id = sourceKnownEntityIdUtils.Validate<User>(externalGuid);
```

#### 2. Repository

Directly available on `ISourceKnownRepository<TEntity>` to standardize validation at the data entry point.

```csharp
var id = userRepository.GetEntityId(externalGuid); 
```

#### 3. Domain Entity

Helper methods on the `SourceKnownEntity` base class, optimized for intra-domain operations.

```csharp
var id = userInstance.GetEntityId<User>(externalGuid);
```

### Secure ↔ Plain Conversion

Entities, repositories, and the injectable utility all expose `ToSecure` / `ToPlain` to convert between encrypted and plaintext `SourceKnownEntityId` forms. Conversion is idempotent — calling `ToSecure` on an already-secure ID returns the same ID.

```csharp
// On entity (after interceptor wiring)
var secureId = userInstance.ToSecure(plainEntityId);
var plainId = userInstance.ToPlain(secureEntityId);

// On repository
var secureId = userRepository.ToSecure(entityId);
var plainId = userRepository.ToPlain(entityId);

// Via injectable utility
var secureId = sourceKnownEntityIdUtils.ToSecure(entityId);
var plainId = sourceKnownEntityIdUtils.ToPlain(entityId);
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
    long Id,                  // The internal database ID (64-bit integer)
    DateTimeOffset CreatedAt, // Timestamp with 250ms tick precision
    uint InstanceId,          // Derived 13-bit topology (7-bit App ID + 6-bit App Instance ID)
    byte AppId,               // Application ID (max 127)
    byte AppInstanceId        // Application Instance ID (max 63)
);

public readonly record struct SourceKnownEntityId(
    SourceKnownId Source,   // The decoded internal components
    Guid EntityId,          // The external opaque GUID
    byte EntityType,        // The entity type identifier (from EntityTypeAttribute)
    bool Valid,             // Whether the ID structure is valid
    bool Secure             // True when EntityId is the encrypted external form
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

- **Standardized Access**: Common CRUD operations (`CreateAsync`, `GetAsync`, `DeleteAsync`).
- **Identity Conversion**: Specialized methods for mapping external `Guid` to internal `SourceKnownEntityId`.
- **Secure ↔ Plain Conversion**: `ToSecure` / `ToPlain` for converting between encrypted and plaintext entity IDs.
- **Cancellation**: `CancellationToken` exposes the stable repository-group token. `CancelWhen(token)` links a lifetime token explicitly, while `CancelChanges` cancels only that named group without canceling the root or unrelated repository keys.
- **Streaming**: Supports `IAsyncEnumerable` for efficient large dataset processing.

The default EntityFramework implementation creates one named child scope per concrete repository type within the parent DI scope. Same-type instances share cancellation. Implementations can join a different intentional group by overriding the non-nullable `RepositoryCancellationScopeKey`. A root-wide cancel remains explicit through the injected utility: `cancellation.Root.Cancel()`.

For one-operation cancellation, link locally instead of creating a dynamic named scope:

```csharp
using var operationSource =
    CancellationTokenSource.CreateLinkedTokenSource(
        repository.CancellationToken,
        operationToken);

await ExecuteAsync(operationSource.Token);
```

Repository contract excerpt:

```csharp
public interface ISourceKnownRepository<TEntity> where TEntity : AggregateRoot
{
    RepositorySettings<TEntity> Settings { get; set; }
    CancellationToken CancellationToken { get; }
    void CancelWhen(CancellationToken token);
    void CancelChanges();
    Task<int> SaveChangesAsync();

    // Predicates & Counts
    Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null);
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null);
    
    // Identity Conversion & Validation
    SourceKnownEntityId GetEntityId(Guid id, bool validate = true);
    SourceKnownEntityId? GetEntityId(Guid? id, bool validate = true);
    SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id) where TOtherEntity : SourceKnownEntity;
    SourceKnownEntityId? GetEntityId<TOtherEntity>(Guid? id) where TOtherEntity : SourceKnownEntity;
    SourceKnownEntityId[] GetEntityIds(IReadOnlyCollection<Guid> ids, bool validate = true);
    SourceKnownEntityId?[] GetEntityIds(IReadOnlyCollection<Guid?> ids, bool validate = true);
    SourceKnownEntityId[] GetEntityIds<TOtherEntity>(IReadOnlyCollection<Guid> ids) where TOtherEntity : SourceKnownEntity;
    SourceKnownEntityId?[] GetEntityIds<TOtherEntity>(IReadOnlyCollection<Guid?> ids) where TOtherEntity : SourceKnownEntity;
    IEnumerable<SourceKnownEntityId> GetEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool validate = true);
    IEnumerable<SourceKnownEntityId?> GetEntityIdsAsEnumerable(IEnumerable<Guid?> ids, bool validate = true);
    IEnumerable<SourceKnownEntityId> GetEntityIdsAsEnumerable<TOtherEntity>(IEnumerable<Guid> ids) where TOtherEntity : SourceKnownEntity;
    IEnumerable<SourceKnownEntityId?> GetEntityIdsAsEnumerable<TOtherEntity>(IEnumerable<Guid?> ids) where TOtherEntity : SourceKnownEntity;
    SourceKnownEntityId ToSecure(SourceKnownEntityId id);
    SourceKnownEntityId ToPlain(SourceKnownEntityId id);
    
    // Data Access
    Task<TEntity[]> GetAllAsync();
    Task<TEntity> GetAsync(Guid id);
    Task<TEntity> GetAsync(SourceKnownEntityId id);
    Task<TEntity?> GetOrDefaultAsync(Guid id, bool validate = true);
    Task<TEntity?> GetOrDefaultAsync(SourceKnownEntityId id, bool validate = true);
    
    // Batch Retrieval
    Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids);
    Task<TEntity[]> GetAsync(IReadOnlyCollection<SourceKnownEntityId> ids);
    // Modification
    void Add(params IReadOnlyCollection<TEntity> entities);
    void Remove(params IReadOnlyCollection<TEntity> entities);
    Task<int> CreateAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<Guid> ids);
    Task<int> DeleteAsync(params IReadOnlyCollection<SourceKnownEntityId> ids);

    // Pagination
    Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
    Task<PaginationResultModel<TEntity>> PaginateAsync(
        PaginationResultInfo? resultInfo = null, long jumpTo = 1, int pageSize = -1, int maxSize = -1,
        PageSortDirection direction = PageSortDirection.None, long totalCount = -1, bool updateTotalCount = false);
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
| `ExceptionFor.Jackpot(msg)` | `JackpotException` | **500** |
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

`JsonConventions` centralizes `System.Text.Json` settings. These defaults are applied by `DrnTestContext` in tests and by `DrnProgramBase` / `DRN.Framework.Hosting` MVC setup in hosted apps.

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

## Shared Extensions

SharedKernel owns low-level extensions needed without higher-layer dependencies.

```csharp
using DRN.Framework.SharedKernel.Extensions;

var schema = "OrderHistory".ToSnakeCase();     // order_history
var typeName = "sample hosted".ToPascalCase(); // SampleHosted

var root = "/data/app";
var file = root.GetPathWithinDirectory("exports", "orders.json");
```

`GetPathWithinDirectory()` rejects parent-directory and symbolic-link traversal. Use it for file-serving, manifest, upload, and app-data child paths.

---

## AppConstants

```csharp
namespace DRN.Framework.SharedKernel;

public static class AppConstants
{
    public static int ProcessId { get; } = Environment.ProcessId;
    public static Guid AppInstanceId { get; } = Guid.NewGuid();
    public static string EntryAssemblyName { get; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "Entry Assembly Not Found";
    public static string EntryAssemblyNameNormalized { get; } = EntryAssemblyName.ToPascalCase();
    public static string EntryAssemblyFullName { get; } = Assembly.GetEntryAssembly()?.GetName().FullName ?? "Entry Assembly Not Found";
    public static string LocalAppDataPath { get; } = GetAppSpecificLocalDataPath();
    public static string TempPath { get; } = GetTempPath();
    public static string LocalIpAddress { get; } = GetLocalIpAddress();

    public const string LocalAppDataPathEnvVariable = "DrnAppDataSettings__DataPath";
    public const string TempPathEnvVariable = "DrnAppDataSettings__TempPath";
}
```

`TempPath` order: `DrnAppDataSettings__TempPath` -> `DrnAppDataSettings__DataPath/Temp` -> local app data `Temp`. `LocalAppDataPath` order: `DrnAppDataSettings__DataPath` -> app-specific local app data. `IAppData` owns directory creation and cleanup.

---

## Global Usings

Suggested consumer usings for projects that work heavily with SharedKernel types:

```csharp
global using DRN.Framework.SharedKernel.Domain;
global using DRN.Framework.SharedKernel;
global using DRN.Framework.SharedKernel.Extensions;
global using DRN.Framework.SharedKernel.Json;
```

---

## Related Packages

- [DRN.Framework.Utils](https://www.nuget.org/packages/DRN.Framework.Utils/) - Configuration, logging, and DI utilities
- [DRN.Framework.EntityFramework](https://www.nuget.org/packages/DRN.Framework.EntityFramework/) - EF Core integration with DrnContext
- [DRN.Framework.Hosting](https://www.nuget.org/packages/DRN.Framework.Hosting/) - Web application hosting
- [DRN.Framework.Testing](https://www.nuget.org/packages/DRN.Framework.Testing/) - Testing utilities

For complete examples, see [Sample.Hosted](https://github.com/duranserkan/DRN-Project/tree/master/Sample.Hosted).

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/.agent/rules/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
