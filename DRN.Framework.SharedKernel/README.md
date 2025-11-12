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
    private const string JsonHelpersFQN = "System.Net.Http.Json.JsonHelpers";
    private const string JsonHelpersOptions = "s_defaultSerializerOptions";
    private const string DefaultSerializerOptions = "s_defaultOptions";
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    static JsonConventions()
    {
        //https://stackoverflow.com/questions/58331479/how-to-globally-set-default-options-for-system-text-json-jsonserializer
        UpdateDefaultJsonSerializerOptions();
        UpdateHttpClientDefaultJsonSerializerOptions();
    }

    public static readonly JsonSerializerOptions DefaultOptions = SetJsonDefaults();
...
    /// <summary>
    ///   <para>Option values appropriate to Web-based scenarios.</para>
    ///   <para>This member implies that:</para>
    ///   <para>- Property names are treated as case-insensitive.</para>
    ///   <para>- "camelCase" name formatting should be employed.</para>
    ///   <para>- Quoted numbers (JSON strings for number properties) are allowed.</para>
    /// </summary>
    public static JsonSerializerOptions SetJsonDefaults(JsonSerializerOptions? options = null)
    {
        options ??= new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        options.AllowTrailingCommas = true;
        options.PropertyNameCaseInsensitive = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

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

## AppConstants

```csharp
namespace DRN.Framework.SharedKernel;

public static class AppConstants
{
    public static int ProcessId { get; } = Environment.ProcessId;
    public static Guid ApplicationId { get; } = Guid.NewGuid();
    public static string EntryAssemblyName { get; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "Entry Assembly Not Found";
    public static string TempPath { get; } = GetTempPath(); //Cleans directory at every startup
    public static string LocalIpAddress { get; } = GetLocalIpAddress();
...
}
```

## Domain Base Definitions

Following definitions provide guidance and conventions for rapid and effective domain design. Their usage is not necessary however other framework features such
as DrnContext benefits from them.

* An entity should always have an long Id. It can have an additional ids.
* All DateTime values should include offset
* Entities should encapsulate their events
* Entity status changes should be marked automatically by dbContext
* Domain events should be published automatically by dbContext

```csharp
namespace DRN.Framework.SharedKernel.Domain;

public abstract class AggregateRoot : Entity;

public abstract class Entity
{
    private List<IDomainEvent> DomainEvents { get; } = new();
    public IReadOnlyList<IDomainEvent> GetDomainEvents() => DomainEvents;
    public long Id { get; protected set; }

    [ConcurrencyCheck]
    public DateTimeOffset ModifiedAt { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }

    protected void AddDomainEvent(DomainEvent? e)
    {
        if (e != null) DomainEvents.Add(e);
    }

    public void MarkAsCreated()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(GetCreatedEvent());
    }

    public void MarkAsModified()
    {
        ModifiedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(GetModifiedEvent());
    }

    public void MarkAsDeleted()
    {
        AddDomainEvent(GetDeletedEvent());
    }

    protected abstract EntityCreated? GetCreatedEvent();
    protected abstract EntityModified? GetModifiedEvent();
    protected abstract EntityDeleted? GetDeletedEvent();

    private bool Equals(Entity other) => Id == other.Id;
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is Entity other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
```

```csharp
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
```

---
**Semper Progressivus: Always Progressive**