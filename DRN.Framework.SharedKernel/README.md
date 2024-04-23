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

## AppConstants

```csharp
namespace DRN.Framework.SharedKernel;

public static class AppConstants
{
    public static int ProcessId { get; } = Environment.ProcessId;
    public static Guid Id { get; } = Guid.NewGuid();
    public static string ApplicationName { get; } = Assembly.GetEntryAssembly()?.GetName().Name
                                                    ?? "Entry Assembly Not Found";

    public static string TempPath { get; } = GetTempPath(); //Cleans directory at every startup
    public static string LocalIpAddress { get; } = GetLocalIpAddress();
...
}
```

## JsonConventions

System.Text.Json defaults will be overridden by JsonConventions when
  * TestContext is used in tests
  * DrnHostBuilder is used to build host

```csharp
namespace DRN.Framework.SharedKernel.Conventions;

public static class JsonConventions
{
...
    static JsonConventions()
    {
        //https://stackoverflow.com/questions/58331479/how-to-globally-set-default-options-for-system-text-json-jsonserializer
        UpdateDefaultJsonSerializerOptions();
        UpdateHttpClientDefaultJsonSerializerOptions();
    }
    
    public static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        AllowTrailingCommas = true
    };
...
}
```


## Exceptions

Following exceptions are used in DRN.Framework and DRN.Nexus and can be used any project. DRN exceptions contain additional category property so that same
exception types can be differentiated with a subcategory.

```csharp
namespace DRN.Framework.SharedKernel;

public abstract class DrnException(string message, Exception exception = null!, string? category = "default") : Exception(message, exception)
{
    public string Category { get; } = category ?? "default";
}

public class ValidationException(string message, Exception exception = null!, string? category = null) : DrnException(message, exception, category);

public class NotSavedException(string message, Exception exception = null!, string? category = null) : DrnException(message, exception, category);

public class NotFoundException(string message, Exception exception = null!, string? category = null) : DrnException(message, exception, category);

public class ExpiredException(string message, Exception exception = null!, string? category = null) : DrnException(message, exception, category);

public class ConfigurationException(string message, Exception exception = null!, string? category = null) : DrnException(message, exception, category);
```

---
**Semper Progredi: Always Progressive**