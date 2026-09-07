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

- **No runtime package dependencies** - Targets .NET 10; includes build-time domain analyzers
- **Domain primitives** - `SourceKnownEntity`, `AggregateRoot`, `DomainEvent` for DDD patterns
- **Typed exceptions** - `ExceptionFor` creates exceptions with status codes consumed by DRN Hosting
- **JSON conventions** - Global `System.Text.Json` defaults with camelCase, enums-as-strings
- **Shared extensions** - Casing and safe path helpers for lower-layer packages
- **Source Known IDs** - Internal `long` keys and external `Guid` identifiers with type and partition validation

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

Define an entity and collect a domain event. SharedKernel supplies the domain types; EntityFramework integration assigns missing IDs during save and wires ID operations during save or materialization.

```csharp
using DRN.Framework.SharedKernel.Domain;

[EntityType<DefaultApp>(1)] // Unique byte identifier for this entity type
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

The constructor alone does not assign `EntityId` or `CreatedAt`. The event reads `EntityId` from its entity, so it observes the ID assigned later. Event collection does not publish the event.

## QuickStart: Advanced

Use the `User` above with a repository implementation. This service maps entities to DTOs, filters by creation date, and reports missing users:

```csharp
using System;
using System.Threading.Tasks;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;

public class UserDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public string Name { get; init; } = string.Empty;
}

public class UserService(ISourceKnownRepository<User> repository)
{
    public async Task<PaginationResultModel<UserDto>> GetUsersAsync(PaginationRequest request)
    {
        var filter = EntityCreatedFilter.After(DateTimeOffset.UtcNow.AddDays(-30));
        var result = await repository.PaginateAsync(request, filter);

        return result.ToModel(user => new UserDto(user) { Name = user.Name });
    }

    public async Task<UserDto> GetUserAsync(Guid id)
    {
        var user = await repository.GetOrDefaultAsync(id);
        if (user is null)
            throw ExceptionFor.NotFound($"User {id} not found");

        return new UserDto(user) { Name = user.Name };
    }
}
```

---

## Domain Primitives

Domain types live in `DRN.Framework.SharedKernel.Domain`. Persistence integration belongs to `DRN.Framework.EntityFramework` and `DrnContext`.

### Entity and AggregateRoot

- **`SourceKnownEntity`**: Holds internal identity, external identity, domain events, and audit timestamps.
- **`AggregateRoot`**: Marker class for DDD aggregate roots.
- **`AggregateRoot<TModel>`**: Adds a writable `Model` through `IEntityWithModel<TModel>`, where `TModel : class`. `Model` starts as `null!` and must be populated by the application.

Entity and aggregate base constructors accept an optional internal `long id = 0`.

| Entity member | Contract |
|---|---|
| `long Id` | Internal setter; ignored by JSON; column order `IdColumnOrder = 0` |
| `Guid EntityId` | External ID from `EntityIdSource`; serialized under the name `Id` |
| `DateTimeOffset CreatedAt` | Derived from the encoded ID timestamp |
| `DateTimeOffset ModifiedAt` | Protected internal setter; concurrency check; `ModifiedAtColumnOrder = 1` |
| `SourceKnownEntityId EntityIdSource` | Internal setter; ignored by JSON |

`SourceKnownEntity` implements `IHasEntityId`, `IEquatable<SourceKnownEntity>`, and `IComparable<SourceKnownEntity>`. See [SourceKnownEntity.cs](Domain/SourceKnownEntity.cs) and [AggregateRoot.cs](Domain/AggregateRoot.cs).

### Application Partitions

`IAppId` requires `static abstract byte AppId { get; }` in the range `0..127`. `[EntityType<TApp>(byte)]` binds an entity type byte to that application partition. The non-generic `EntityTypeAttribute` is abstract. Attributes are not inherited, and each entity can declare only one.

| Partition | `AppId` and `Value` | Attribute example |
|---|---|---|
| `DefaultApp` | 0 | `[EntityType<DefaultApp>(1)]` for standalone domains |
| `NexusApp` | 126 | `[EntityType<NexusApp>(1)]`; Nexus also supplies a domain-derived `NexusEntityTypeAttribute` accepting `NexusEntityTypes` |
| `TestApp` | 127 | `[TestEntityType(1)]`, equivalent to `[EntityType<TestApp>(1)]` |

`IAppId.DefaultAppId`, `NexusAppId`, and `TestAppId` expose these constants; `MaxAppId` is 127. Different partitions may reuse an entity type byte. See [IAppId.cs](Domain/IAppId.cs).

### Compile-Time Roslyn Analyzers

`DRN.Framework.SharedKernel` includes built-in Roslyn analyzers (`DRN.Framework.SharedKernel.Analyzers`) delivering compile-time domain validation transitively to referencing projects and NuGet consumers:

| Diagnostic ID | Severity | Title | Description |
|---|---|---|---|
| **DRN0001** | Error | Missing `[EntityType]` attribute | Concrete, effectively non-private `SourceKnownEntity` classes must declare `[EntityType<TApp>(byte)]` or a supported derived domain attribute. |
| **DRN0002** | Error | Duplicate `EntityType` value | Every entity class in the domain compilation and referenced assemblies must have a unique `EntityType` byte value per `AppId`. |
| **DRN0003** | Error | Invalid `[EntityType]` usage | `[EntityType]` attribute must not be placed on abstract classes, private classes, or non-`SourceKnownEntity` types. |
| **DRN0004** | Warning | Duplicate entity class name | Warns when multiple entities across the domain model share identical unqualified class names within the same `AppId` to prevent EF Core and messaging collisions. |
| **DRN0005** | Error | Multiple `AppId`s in single compilation | Allows at most one non-test partition unless a multi-application or test exemption applies. `TestApp` (127) is excluded from the count. |
| **DRN0006** | Error | Unresolvable or non-constant `AppId` in `[EntityType]` | Enforces that `IAppId` implementations declare a constant value (`public const byte Value = ...;` or `public const byte AppId = ...;`) so partition identities can be read from metadata across assembly boundaries. |
| **DRN0007** | Error | `AppId` outside the supported range | Enforces that statically resolved `IAppId` values used by `[EntityType]` declarations are between 0 and 127, matching Source-Known ID runtime constraints. |
| **DRN0008** | Error | Unsupported entity attribute constructor | Derived attributes must reach `EntityTypeAttribute<TApp>` through one constructor per class, with one byte or byte-backed enum parameter forwarded unchanged to the base constructor. |

Derived entity attributes use a pass-through constructor:

```csharp
using DRN.Framework.SharedKernel.Domain;

public enum DomainEntityTypes : byte { Order = 2 }

public sealed class DomainEntityTypeAttribute(DomainEntityTypes kind)
    : EntityTypeAttribute<DefaultApp>((byte)kind);
```

Parameter names may differ. Primary and ordinary constructors are supported. Reordered, additional, fixed-value, overloaded, or transformed mappings produce `DRN0008`. `AppId` comes from the generic `TApp` binding. A derived property hiding `AppId` does not change it.

The analyzer checks source attribute declarations even when no entity uses them yet. For compiled references, Roslyn exposes constructor signatures but not bodies. Signatures are checked; forwarding relies on producer-side validation. Run the analyzer when building domain attribute libraries. It cannot prove forwarding in a precompiled library built with validation disabled.

An entity is effectively private if it or any containing type is private. Such entities are excluded from required-attribute and collision checks in both local and referenced analysis; annotating one locally reports `DRN0003`.

- **Cross-assembly checks**: Hosts and aggregators detect collisions across referenced domain modules at compilation end. Diamond dependencies such as `A -> B -> Common` and `A -> C -> Common` are deduplicated by Roslyn symbol equality.
- **DRN0005 exemptions**: Set `<AllowMultipleAppIds>true</AllowMultipleAppIds>`, `<IsTestProject>true</IsTestProject>`, or `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>`. Test assembly names also qualify when they contain `.Test.`, start with `Test.`, or end with `.Tests` or `.Test`, case-insensitively.

See [SourceKnownEntityTypeAnalyzer.cs](../DRN.Framework.SharedKernel.Analyzers/SourceKnownEntityTypeAnalyzer.cs) for diagnostic checks and [the package project](DRN.Framework.SharedKernel.csproj) for analyzer packaging.

> [!IMPORTANT]
> **Identity Rule**: Always use `Guid EntityId` (mapped as `Id` in DTOs) for all public-facing contracts, API route parameters, and external lookups. The internal `long Id` must **never** be exposed outside the infrastructure/domain boundaries.
>
> **DTO Mapping Rule**: DTOs should implement a primary constructor accepting a `SourceKnownEntity?` to automatically map `Id`, `CreatedAt`, and `ModifiedAt`. Avoid manual mapping of these fields.
>
> **Entity Exposure Prohibition**: Entities must never be exposed via public APIs. Always map to DTOs or Response Models. Entities are only permitted in Razor Pages (Internal UI).

The advanced quickstart defines `UserDto` once and uses its base constructor for identity and timestamp mapping. `Dto.AdditionalData` stores JSON extension data. See [Dto.cs](Domain/Dto.cs).

### Domain Events

Entities collect events through `AddDomainEvent` and expose them through `GetDomainEvents`. SharedKernel does not supply publication or outbox dispatch. EF save interception invokes lifecycle hooks; override `GetCreatedEvent`, `GetModifiedEvent`, or `GetDeletedEvent` to supply events. Each hook returns null by default.

Event contract excerpt from [DomainEvent.cs](Domain/DomainEvent.cs):

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

### SourceKnownEntityId & SourceKnownId

The identifier system has three forms:

| Form | Representation | Purpose |
|---|---|---|
| Source Known ID (SKID) | 64-bit `long`, 8 bytes | Sortable database key containing creation time, application, instance and sequence fields |
| Source Known Entity ID (SKEID) | 128-bit `Guid` | Adds entity type, an epoch byte and a 4-byte keyed BLAKE3 MAC for validation without a database lookup |
| Secure SKEID | 128-bit `Guid` | Encrypts one SKEID block with AES-256-ECB to conceal its encoded fields |

`ISourceKnownEntityIdOperations` defines `Generate`, `Parse`, `ToSecure`, and `ToPlain` in SharedKernel. Utils implements it through `ISourceKnownEntityIdUtils` and `SourceKnownEntityIdUtils`. EF interceptors wire operations into entities. ID validation checks structure and identity metadata; application authorization still controls access to the entity.

Record contract excerpt from [SourceKnownEntityId.cs](Domain/SourceKnownEntityId.cs):

```csharp
public readonly record struct SourceKnownId(
    long Id,
    DateTimeOffset CreatedAt, // 250ms tick precision
    uint InstanceId,          // 18-bit per-tick sequence
    byte AppId,               // 7 bits, 0..127
    byte AppInstanceId        // 6 bits, 0..63
);

public readonly record struct SourceKnownEntityId(
    SourceKnownId Source,
    Guid EntityId,
    byte EntityType,
    bool Valid,
    bool Secure
);
```

`Valid` reports parsing validity. `Secure` identifies the encrypted form of `EntityId`. `EntityTypeId` combines `EntityType` and `Source.AppId`. The sequence layout is implemented in [SourceKnownIdUtils.cs](../DRN.Framework.Utils/Ids/SourceKnownIdUtils.cs); MAC and encryption belong to [SourceKnownEntityIdUtils.cs](../DRN.Framework.Utils/Ids/SourceKnownEntityIdUtils.cs).

### ID Validation & Retrieval Strategies

| Context | Call | Requirement |
|---|---|---|
| Service | `sourceKnownEntityIdUtils.Validate<User>(externalGuid)` | Inject the Utils ID utility |
| Repository | `userRepository.GetEntityId(externalGuid)` | Use `ISourceKnownRepository<User>` |
| Domain entity | `userInstance.GetEntityId<User>(externalGuid)` | Entity ID assigned and ID operations wired |

`GetEntityId` helpers throw `UnprocessableEntityException` while the current entity is pending insertion. Missing ID operations cause `ConfigurationException`. For a parsed `SourceKnownEntityId`, choose validation by the expected boundary:

| Method | Checks |
|---|---|
| `ValidateId()` | `Valid` is true |
| `Validate<TEntity>()` | Validity, entity type and the entity's declared application partition |
| `Validate(EntityTypeId expected)` | Validity and both supplied identity components |
| `Validate(byte entityType)` | Validity and entity type, using the ID's own `AppId` |

Use the generic or composite overload when the expected partition must be checked independently.

### Secure ↔ Plain Conversion

Entities, repositories and the Utils utility expose `ToSecure` and `ToPlain`. Conversion validates the ID and is idempotent: an ID already in the requested form is returned unchanged. Entity conversion requires wired ID operations.

The following alternatives assume the named entity, repository or utility is available:

```csharp
var secureFromEntity = userInstance.ToSecure(entityId);
var plainFromEntity = userInstance.ToPlain(secureFromEntity);

var secureFromRepository = userRepository.ToSecure(entityId);
var plainFromRepository = userRepository.ToPlain(secureFromRepository);

var secureFromUtility = sourceKnownEntityIdUtils.ToSecure(entityId);
var plainFromUtility = sourceKnownEntityIdUtils.ToPlain(secureFromUtility);
```

---

## SourceKnownRepository

`ISourceKnownRepository<TEntity>` defines queries, mutations, identity conversion and pagination for `AggregateRoot` entities. EntityFramework supplies the default implementation. Subclasses own entity updates, additional filters and query includes.

Contract from [SourceKnownRepository.cs](Domain/Repository/SourceKnownRepository.cs), grouped by operation:

```csharp
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;

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

### Settings and Cancellation

`RepositorySettings<TEntity>` provides `IgnoreAutoIncludes`, `AsNoTracking`, `ScopeKey`, and named predicate filters through `AddFilter`, `RemoveFilter`, and `ClearFilters`. Both boolean settings default to false. See [RepositorySettings.cs](Domain/Repository/RepositorySettings.cs).

- A null `ScopeKey` uses the root cancellation scope. A configured key selects a child scope shared by repositories using that key.
- `CancellationToken` exposes the effective scope token. `CancelChanges()` cancels that scope, and `CancelWhen(token)` links a lifetime token to it.
- Cancellation lasts for the scope's lifetime. With a null key, it affects root-linked operations too.
- For a single operation, create a local linked token source and pass its token to an operation that accepts it. Do not merge an operation-only token through `CancelWhen`.

`CancellationScopeKey` lives in `DRN.Framework.SharedKernel.Cancellation`. Create keys through its `For(...)` factories. See [the key contract](Cancellation/CancellationScopeKey.cs) and [the EntityFramework repository](../DRN.Framework.EntityFramework/Domain/SourceKnownRepository.cs).

### Filtering

`EntityCreatedFilter` supplies `After`, `Before`, `Between`, and `Outside` factories. Each defaults to `inclusive: true`. In the default implementation, inclusive boundaries include the full 250ms ID tick; exclusive boundaries exclude it. `Between` and `Outside` normalize reversed endpoints.

With a repository and pagination request in scope:

```csharp
// Example: Get records created in the last 7 days
var filter = EntityCreatedFilter.After(DateTimeOffset.UtcNow.AddDays(-7));
var result = await repository.PaginateAsync(request, filter);
```

See [EntityCreatedFilter.cs](Domain/Repository/EntityCreatedFilter.cs) and [EntityDateTimeUtils.cs](../DRN.Framework.Utils/Entity/EntityDateTimeUtils.cs).

---

## Pagination

Pagination uses first/last entity IDs as cursors for forward, backward and refresh requests. It does not provide a database snapshot across requests.

| Setting or operation | Behavior |
|---|---|
| `PaginationRequest.From()` | Starts at page 1, size 10, maximum 100, ascending order |
| Changed size, effective maximum or direction | Resets to page 1 with a fresh cursor |
| Omitted settings during reset | Retains the previous size, maximum and direction |
| Maximum size | Capped at 1,000 before comparison; repeating an above-threshold limit preserves navigation |
| `From(resultInfo, jumpTo: ...)` | Limits movement to ten pages in the requested direction; 100 to 1 targets 90, and 1 to 100 targets 11 |
| `RequestNextPage()`, `RequestPreviousPage()`, `RequestRefresh()` | Creates navigation requests from result metadata |
| `RequestPage(n)` | Creates a direct page request; does not apply `From`'s ten-page clamp |
| `TotalCount`, `UpdateTotalCount` | Carries a known count or requests recalculation; `-1` means unspecified |
| `PaginateAllAsync` | Streams pages as `IAsyncEnumerable<PaginationResultModel<TEntity>>` |

The three-argument `PageSize` constructor can override the maximum threshold for in-process requests. That override is not serializable. See [PaginationRequest.cs](Domain/Pagination/PaginationRequest.cs), [PageSize.cs](Domain/Pagination/PageSize.cs), and [PaginationResultBase.cs](Domain/Pagination/PaginationResultBase.cs).

### API Integration

`PaginationRequest` supports query-string model binding. This controller uses the quickstart's `User` and `UserDto`:

```csharp
using System.Threading.Tasks;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("users")]
public class UsersController(ISourceKnownRepository<User> repository) : ControllerBase
{
    [HttpGet]
    public async Task<PaginationResultModel<UserDto>> GetAsync([FromQuery] PaginationRequest request)
    {
        var result = await repository.PaginateAsync(request);
        return result.ToModel(user => new UserDto(user) { Name = user.Name });
    }
}
```

### Usage

```csharp
// Inside a service method with repository in scope.
var request = PaginationRequest.DefaultWith(size: 20, direction: PageSortDirection.Descending);
var result = await repository.PaginateAsync(request);

if (result.Info.HasNext)
{
    var nextRequest = result.Info.RequestNextPage();
    var nextPage = await repository.PaginateAsync(nextRequest);
}
```

---

## Exceptions

`ExceptionFor` creates `DrnException` subclasses with `Status` and `Category`. DRN Hosting's `HttpScopeMiddleware` maps the status to an HTTP response and aborts invalid status values. SharedKernel alone does not handle HTTP requests.

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

Every factory accepts `string message`, optional `Exception? exception = null`, and optional `string? category = "default"`. The base exception defaults to status 500 and category `DrnException.DefaultCategory` (`"default"`). It also exposes a string-keyed `Data` dictionary. `MaliciousRequestException.Status` is `short.MaxValue`, the abort sentinel used by Hosting.

```csharp
using DRN.Framework.SharedKernel;

throw ExceptionFor.NotFound("User not found", category: "Users");
```

See [Exceptions.cs](Exceptions.cs) and [HttpScopeMiddleware.cs](../DRN.Framework.Hosting/Middlewares/HttpScopeMiddleware.cs).

---

## JsonConventions

`JsonConventions.DefaultOptions` centralizes `System.Text.Json` settings. Its static initializer replaces the serializer's static default option instances. Utils convention setup initializes it, including setup used by `DrnTestContext`. Hosting also configures HTTP JSON and MVC options.

| Setting | Value |
|---|---|
| New options | `JsonSerializerDefaults.Web` |
| Property names | camelCase, case-insensitive reading |
| Enums | `JsonStringEnumConverter` |
| Trailing commas | Allowed |
| Number handling | `AllowReadingFromString` |
| Maximum depth | 32 |
| `long` and `long?` | JSON numbers within `-9,007,199,254,740,991..9,007,199,254,740,991`; strings outside that range |

`Int64ToStringConverter` and `Int64NullableToStringConverter` accept numeric tokens and integer strings. The nullable converter preserves null. `SetJsonDefaults(options)` updates supplied mutable options or creates new options when omitted. `SetHtmlSafeWebJsonDefaults(options)` also sets `JavaScriptEncoder.Default`; Hosting uses it for MVC.

```csharp
using System.Text.Json;
using DRN.Framework.SharedKernel.Json;

var json = JsonSerializer.Serialize(new { Count = long.MaxValue }, JsonConventions.DefaultOptions);
// {"count":"9223372036854775807"}
```

See [JsonConventions.cs](Json/JsonConventions.cs) and [IntegerSafeIntervalForJs.cs](Json/IntegerSafeIntervalForJs.cs).

---

## Attributes

### `[IgnoreLog]`

Marks classes, structs, properties or fields for exclusion by logging code that honors the attribute. `IgnoredLog(object?)` checks the runtime type and returns false for null. `IgnoredLog(PropertyInfo)` also ignores properties typed as `object` or carrying an ignored property type. See [IgnoreLogAttribute.cs](Attributes/IgnoreLogAttribute.cs).

### `[SecureKey]`

Validates string properties, fields or parameters. Defaults require 16 to 256 characters, uppercase, lowercase, a digit and a special character. The allowed set contains ASCII and Turkish letters, digits, space, and `!*()-_`; space counts as special. Sequential and repeated-character limits are configurable through `MaxSequentialChars` (4) and `MaxRepeatedChars` (3). Null values fail validation. See [SecureKeyAttribute.cs](Attributes/SecureKeyAttribute.cs) for the exact character checks.

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

`GetPathWithinDirectory()` resolves a full path, rejects paths outside the root, and rejects symbolic links or reparse points in child components below that root. Use it for file-serving, manifest, upload and app-data child paths. It checks the path at resolution time; it does not lock the filesystem against later changes. `IsPathWithinDirectory()` checks lexical containment only and does not resolve symbolic links.

See [StringExtensions.cs](Extensions/StringExtensions.cs) and [PathExtensions.cs](Extensions/PathExtensions.cs).

---

## AppConstants

`AppConstants` is in `DRN.Framework.SharedKernel`. Values are initialized once from the process, entry assembly, environment and local network:

| Property | Value |
|---|---|
| `int ProcessId` | `Environment.ProcessId` |
| `Guid AppInstanceId` | A new GUID for this process initialization |
| `string EntryAssemblyName` | Entry assembly name, or `"Entry Assembly Not Found"` |
| `string EntryAssemblyNameNormalized` | Entry assembly name converted with `ToPascalCase()` |
| `string EntryAssemblyFullName` | Full entry assembly name, or `"Entry Assembly Not Found"` |
| `string LocalAppDataPath` | Configured data path or application-specific local data directory |
| `string TempPath` | Application-specific temp directory from the fallback order below |
| `string LocalIpAddress` | Local IPv4 address, falling back to loopback |

`LocalAppDataPathEnvVariable` is `DrnAppDataSettings__DataPath`. `TempPathEnvVariable` is `DrnAppDataSettings__TempPath`. These are process environment overrides, resolved before DRN configuration.

`TempPath` appends `EntryAssemblyNameNormalized` in this order:

1. `DrnAppDataSettings__TempPath/<EntryAssemblyNameNormalized>`
2. `DrnAppDataSettings__DataPath/Temp/<EntryAssemblyNameNormalized>`
3. `<LocalApplicationData>/Temp/<EntryAssemblyNameNormalized>`

`LocalAppDataPath` uses `DrnAppDataSettings__DataPath` as configured, otherwise `<LocalApplicationData>/<EntryAssemblyNameNormalized>`. Path resolution can return an empty string when no usable root is available. `IAppData` in Utils owns directory creation, temp cleanup, test temp preservation and safe child paths.

See [AppConstants.cs](AppConstants.cs) and [AppData.cs](../DRN.Framework.Utils/Data/App/AppData.cs).

---

## Global Usings

Suggested consumer usings for projects that work heavily with SharedKernel types:

```csharp
global using DRN.Framework.SharedKernel.Domain;
global using DRN.Framework.SharedKernel.Domain.Pagination;
global using DRN.Framework.SharedKernel.Domain.Repository;
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
