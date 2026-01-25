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

DRN.Framework.SharedKernel package is an lightweight package that contains common codes suitable for contract and domain layers. It can be referenced by any
projects such as other DRN.Framework packages, projects developed with DRN.Framework.

## JsonConventions

System.Text.Json defaults will be overridden by JsonConventions when
* DrnTestContext is used in tests
* DrnHostBuilder is used to build host

```csharp
namespace DRN.Framework.SharedKernel.Json;

public static class JsonConventions
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    static JsonConventions()
    {
        //https://stackoverflow.com/questions/58331479/how-to-globally-set-default-options-for-system-text-json-jsonserializer
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
        options.TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault
            ? new DefaultJsonTypeInfoResolver()
            : JsonTypeInfoResolver.Combine();

        return options;
    }

    public static JsonSerializerOptions SetHtmlSafeWebJsonDefaults(JsonSerializerOptions? options = null)
    {
        options = SetJsonDefaults(options);
        options.Encoder = JavaScriptEncoder.Default;

        return options;
    }
}
```


## Exceptions

`DrnException` are used in DRN.Framework and DRN.Nexus and can be used any project. These exceptions contain additional category property so that same
exception types can be differentiated with a subcategory. They also contain status code so that HttpScopeHandler can respect that.

`ExceptionFor` factory class groups all DrnExceptions.

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
    public new IDictionary<string, object> Data { get; } = new Dictionary<string, object>();
}

/// <summary>
/// Scope handler returns 400 when thrown
/// </summary>
public class ValidationException(string message, Exception? ex = null, string? category = null)
    : DrnException(message, ex, category, 400);
...
    
public static class ExceptionFor
{
    private const string Default = DrnException.DefaultCategory;

    /// <summary>
    /// Scope handler returns 400 when thrown
    /// </summary>
    public static ValidationException Validation(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 401 when thrown
    /// </summary>
    public static UnauthorizedException Unauthorized(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 403 when thrown
    /// </summary>
    public static ForbiddenException Forbidden(string message, Exception? exception = null, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 404 when thrown
    /// </summary>
    public static NotFoundException NotFound(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 409 when thrown
    /// </summary>
    public static ConflictException Conflict(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 410 when thrown
    /// </summary>
    public static ExpiredException Expired(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 500 when thrown
    /// </summary>
    public static ConfigurationException Configuration(string message, Exception? ex = null, string? category = Default)
        => new(message, ex, category);

    /// <summary>
    /// Scope handler returns 422 when thrown
    /// </summary>
    public static UnprocessableEntityException UnprocessableEntity(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// To abort requests that doesn't even deserve a result
    /// </summary>
    public static MaliciousRequestException MaliciousRequest(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);
}
```

## Attributes

When an object, property, or field is annotated with the IgnoreLog attribute, the IScopeLog implementation detects this attribute and excludes the marked element from the log data.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
public class IgnoreLogAttribute : Attribute;

public static class IgnoreLogExtensions
{
    public static bool IgnoredLog(this object obj) => obj.GetType().GetCustomAttributes()
        .Any(attribute => attribute.GetType() == typeof(IgnoreLogAttribute));

    public static bool IgnoredLog(this PropertyInfo info) =>
        info.PropertyType == typeof(object) ||
        info.GetCustomAttributes().Union(info.PropertyType.GetCustomAttributes())
            .Any(attribute => attribute.GetType() == typeof(IgnoreLogAttribute));
}
```

### SecureKeyAttribute

Validates that a string meets secure key requirements (length, character classes, allowed characters).

```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SecureKeyAttribute() : ValidationAttribute(DefaultErrorMessage)
{
    public ushort MinLength { get; set; } = 16;
    public ushort MaxLength { get; set; } = 256;
    // ... configuration properties
}
```

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
...
}
```

## Domain Base Definitions

Following definitions provide guidance and conventions for rapid and effective domain design. Their usage is not necessary however other framework features such
as DrnContext benefits from them.

* **SourceKnownEntity**: The base class for entities. It handles identity (long Id for DB, Guid EntityId for external), domain events, and auditing.
* **EntityTypeAttribute**: Defines a stable, unique byte identifier for each entity type.
* **Domain Events**: Entities encapsulate their events. `DrnContext` publishes them automatically.

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

    private List<IDomainEvent> DomainEvents { get; } = new(2);
    public IReadOnlyList<IDomainEvent> GetDomainEvents() => DomainEvents;

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
    [JsonPropertyOrder(-3)]
    public Guid EntityId => EntityIdSource.EntityId;

    [JsonPropertyOrder(-2)]
    public DateTimeOffset CreatedAt => EntityIdSource.Source.CreatedAt;

    [ConcurrencyCheck]
    [JsonPropertyOrder(-1)]
    [Column(Order = ModifiedAtColumnOrder)]
    public DateTimeOffset ModifiedAt { get; protected internal set; }

    [JsonIgnore]
    public SourceKnownEntityId EntityIdSource { get; internal set; }
    
    // ... GetEntityId methods, Equals, CompareTo
}

public abstract class AggregateRoot(long id = 0) : SourceKnownEntity(id);

public abstract class AggregateRoot<TModel>(long id = 0) : AggregateRoot(id), IEntityWithModel<TModel> where TModel : class
{
    public TModel Model { get; set; } = null!;
}
```

```csharp
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

## SourceKnownRepository

`ISourceKnownRepository<TEntity>` defines a standard contract for repositories managing `AggregateRoot` entities. It offers a consistent API for data access, identity management, and unit of work patterns.

*   **Standardized Access**: Provides common CRUD operations (`CreateAsync`, `GetAsync`, `DeleteAsync`).
*   **Identity Conversion**: Includes methods to convert between external `Guid`s and internal `SourceKnownEntityId`s using `GetEntityId` helper methods, ensuring IDs are valid and match the expected entity type.
*   **Cancellation Support**: Built-in support for `CancellationToken` propagation.

> [!WARNING]
> `GetAllAsync()` returns all matching entities in a single query. This should be used **only** when the result set is guaranteed to be small (e.g., via repository settings filters) or for specific maintenance tasks. Avoid in public-facing APIs.

```csharp
public interface ISourceKnownRepository<TEntity> where TEntity : AggregateRoot
{
    RepositorySettings<TEntity> Settings { get; set; }
    
    // Identity Conversion & Validation
    SourceKnownEntityId GetEntityId(Guid id, bool validate = true);
    SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id) where TOtherEntity : SourceKnownEntity;
    
    // Data Access
    Task<TEntity?> GetOrDefaultAsync(SourceKnownEntityId id, bool validate = true);
    Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
}
```

## SourceKnownEntity

`SourceKnownEntity` is the base class for all domain entities. It standardizes identity management, domain events, and auditing.

*   **EntityTypeAttribute**: Every `SourceKnownEntity` must be decorated with `[EntityType(byte)]`. This assigns a unique, stable byte identifier to the class, which is used for ID generation and type safety.
*   **ID Conversion**: Provides `GetEntityId` methods to convert internal `long` IDs or external `Guid`s into strongly-typed `SourceKnownEntityId`s.
*   **Validation**:
    *   **Parser & IdFactory**: Internal delegates used to reconstruct IDs.
    *   **Validation**: `GetEntityId` methods include a `bool validate = true` parameter (default). When true, it verifies the ID's structure and ensures the entity type matches the expected type.

```csharp
[EntityType(1)] // Unique byte identifier for this entity type
public class MyEntity : SourceKnownEntity 
{
    // ...
}

// Usage
var entityId = myEntity.GetEntityId(someGuid, validate: true); // Validates ID structure and EntityType
```

## SourceKnownEntityId & SourceKnownId

The framework uses a composite identifier system to balance database performance with external security and type safety.

### SourceKnownId
The internal structure representing the identity components.
```csharp
public readonly record struct SourceKnownId(
    long Id,                // The internal database ID (64-bit integer)
    DateTimeOffset CreatedAt, // Timestamp when the ID was generated
    uint InstanceId,        // Generator instance ID (for distributed uniqueness)
    byte AppId,             // Application ID
    byte AppInstanceId      // Application Instance ID
);
```

### SourceKnownEntityId
The public-facing identifier wrapper.
```csharp
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

## Pagination

The framework provides a robust pagination system capable of handling large datasets efficiently using cursor-based navigation.

*   **PaginationRequest**: Encapsulates page number, page size, cursor (sorting/filtering position), and optional total count updates.
*   **PaginationResult**: Returns a slice of data along with navigation metadata (`HasNext`, `HasPrevious`, `TotalCount`).
*   **Cursor Support**: Optimizes performance for "Next/Previous" navigation by using stable cursors instead of expensive offset-based skipping.

```csharp
// Example usage in a repository or service
var request = PaginationRequest.DefaultWith(size: 20, direction: PageSortDirection.Descending);
var result = await repository.PaginateAsync(request);

if (result.HasNext) 
{
    // Logic to prepare next page link
}
```

---
**Semper Progressivus: Always Progressive**