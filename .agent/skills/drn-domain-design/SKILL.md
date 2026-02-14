---
name: drn-domain-design
description: Domain-Driven Design implementation patterns - Entity design, Source-Known IDs, repository contracts, domain events, EF Core configuration. Essential for domain modeling and data access. Keywords: ddd, domain-modeling, entity-design, repository, source-known-id, entity-type, domain-events, ef-core-configuration, fluent-api, aggregate-root, pagination, dto-mapping
---

# Domain Design Skill (DRN.Framework)

> Type-safe domain modeling with Source-Known IDs, repository patterns, and EF Core conventions.

## When to Apply
- Defining domain entities and aggregate roots
- Implementing repositories for data access
- Working with Source-Known identity system
- Configuring EF Core entity mappings
- Implementing pagination and filtering in APIs
- Understanding DrnContext conventions

---

## Overview

This skill defines the standards for domain-driven design within DRN.Framework, emphasizing:
- **Type-Safe Identity**: Composite IDs balancing DB performance (long) with external security (GUID)
- **Repository Contracts**: Standard interfaces for data access
- **Entity Life-Cycle**: Automatic tracking and domain events
- **EF Core Conventions**: Attribute-first configuration with Fluent API for complex cases

> [!TIP]
> **Design Preference**: Prefer **Attribute-based configuration** over Fluent API when available. Use Fluent API only for **complex definitions** that cannot be elegantly expressed with attributes (e.g., composite keys, complex many-to-many relationships, or conditional mapping). This keeps standard configuration co-located with implementation while reserving the power of Fluent API for exceptional cases.

## Table of Contents

- [Identity System](#identity-system)
  - [SourceKnownId](#sourceknownid)
  - [SourceKnownEntityId](#sourceknownentityid)
  - [Construction Mechanisms](#construction-mechanisms)
  - [Validation Features](#validation-features)
- [Entity Bases](#entity-bases)
  - [SourceKnownEntity](#sourceknownentity)
  - [AggregateRoot](#aggregateroot)
- [Repository Contract](#repository-contract)
  - [Complete Interface Members](#complete-interface-members)
  - [RepositorySettings](#repositorysettings)
  - [EntityCreatedFilter](#entitycreatedfilter)
- [Data Persistence Conventions](#data-persistence-conventions)
  - [DrnContext Features](#drncontext-features)
  - [Advanced Database Configuration](#advanced-database-configuration)
  - [Model Composition Patterns](#model-composition-patterns)
  - [Configuration Best Practices](#configuration-best-practices)
- [Repository Implementation](#repository-implementation)
  - [SourceKnownRepository](#sourceknownrepository-base-class)
  - [Query Composition Methods](#query-composition-methods)
- [Pagination Models](#pagination-models)
  - [PaginationRequest](#paginationrequest)
  - [PaginationResultInfo](#paginationresultinfo)
  - [PaginationResultModel](#paginationresultmodel)
- [Entity Utilities](#entity-utilities)
- [Public API Integration Standards](#public-api-integration-standards)
- [Related Skills](#related-skills)

---

## Identity System

The framework uses a composite identifier system to balance DB performance (long IDs) with external security (GUIDs) and type safety.

> [!IMPORTANT]
> **External Identity Rule**: Always use `Guid EntityId` (mapped as `Id` in DTOs) for all public-facing contracts, API route parameters, and external lookups. The internal `long Id` must never be exposed outside the infrastructure/domain boundaries. 
> 
> **DTO Mapping Rule**: DTOs **must derive from the `Dto` base class** (`DRN.Framework.SharedKernel.Domain.Dto`). They must use a **primary constructor** (or base constructor) that accepts a `SourceKnownEntity?` to automatically handle `Id`, `CreatedAt`, and `ModifiedAt` mapping. Manual mapping of these infrastructure fields is forbidden to ensure consistency.

### SourceKnownId
The internal structure representing identity components.
```csharp
public readonly record struct SourceKnownId(
    long Id,                  // Internal DB ID (primary key component)
    DateTimeOffset CreatedAt, // Timestamp of generation
    uint InstanceId,          // Generator instance ID
    byte AppId,               // Application ID
    byte AppInstanceId        // Application Instance ID
);
```

### SourceKnownEntityId
The public-facing identifier wrapper used in contracts and domain logic.
```csharp
public readonly record struct SourceKnownEntityId(
    SourceKnownId Source,   // Decoded internal components
    Guid EntityId,          // Opaque external GUID
    byte EntityType,        // Entity type discriminator
    bool Valid              // Structural validity flag
);
```

### Construction Mechanisms
Identity reconstruction is handled via delegates and interceptors:
- **During Materialization**: `IDrnMaterializationInterceptor` initializes `IdFactory` and `Parser` delegates on the entity instance.
- `Parser`: `Func<Guid, SourceKnownEntityId>` - Converts GUID to structured ID.
- `IdFactory`: `Func<long, byte, SourceKnownEntityId>` - Reconstructs ID from long and type.

### Validation Features
- `ValidateId()`: Ensures the ID is structurally valid.
- `Validate<TEntity>()`: Ensures the `EntityType` matches the expected `[EntityType]` attribute of the class.
- `HasSameEntityType<TEntity>()`: Boolean check for type compatibility.

## Entity Bases

### SourceKnownEntity
Base class for all entities (`DRN.Framework.SharedKernel.Domain`).

- **EntityTypeAttribute**: Must be applied to every concrete entity.
  ```csharp
  [EntityType(1)]
  public class MyEntity : SourceKnownEntity { ... }
  ```
  
  > [!WARNING]
  > Each entity must have unique `[EntityType(byte)]` value. DrnContext validates at startup and throws `ConfigurationException` on duplicates.
  
- **Auditing**: Automatically tracks `Id`, `EntityId`, `CreatedAt`, and `ModifiedAt`.
- **Domain Events**: Encapsulates `IDomainEvent` collection.
- **Extended Properties**: Supports JSON-based flexible extensions via `ExtendedProperties` string and `Get/SetExtendedProperties<TModel>` methods.

### AggregateRoot
Marker base for aggregate roots, used for repository constraints.
- `AggregateRoot<TModel>`: Supports a strongly-typed `Model` property for DDD patterns where logic and data models are separated.

## Repository Contract

`ISourceKnownRepository<TEntity>` defines the standard contract for repositories managing `AggregateRoot` entities.

### Complete Interface Members

#### Settings & Context
- `RepositorySettings<TEntity> Settings { get; set; }`: Core settings for query behavior and global filters.
- `CancellationToken CancellationToken { get; set; }`: Managed cancellation token.
- `void MergeCancellationTokens(CancellationToken other)`: Combines external tokens with repository token.
- `void CancelChanges()`: Cancels current operation.
- `Task<int> SaveChangesAsync()`: Persists changes to the underlying store.

#### Predicates & Analytics
- `Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate)`
- `Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null)`
- `Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null)`

#### Retrieval
- `Task<TEntity[]> GetAllAsync()`: ⚠️ Returns all matching entities in a single query. Only safe when bounded by settings filters or small record counts.
- `Task<TEntity> GetAsync(Guid id)` or `GetAsync(SourceKnownEntityId id)`: Retrieves a single entity.
- `Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids)` or `GetAsync(IReadOnlyCollection<SourceKnownEntityId> ids)`: Retrieves multiple entities.
- `Task<TEntity?> GetOrDefaultAsync(Guid id, bool validate = true)` or `GetOrDefaultAsync(SourceKnownEntityId id, bool validate = true)`: Retrieval with null safety.

#### Commands
- `void Add(params IReadOnlyCollection<TEntity> entities)`: Tracks entities for creation.
- `void Remove(params IReadOnlyCollection<TEntity> entities)`: Tracks entities for deletion.
- `Task<int> CreateAsync(params IReadOnlyCollection<TEntity> entities)`: Adds and saves.
- `Task<int> DeleteAsync(params IReadOnlyCollection<TEntity> entities / IReadOnlyCollection<Guid> ids / IReadOnlyCollection<SourceKnownEntityId> ids)`: Removes and saves.

#### Identity Conversion (Guid to SourceKnownEntityId)
- `SourceKnownEntityId GetEntityId(Guid id, bool validate = true)`
- `SourceKnownEntityId? GetEntityId(Guid? id, bool validate = true)`
- `SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id)`
- `SourceKnownEntityId? GetEntityId<TOtherEntity>(Guid? id)`
- `SourceKnownEntityId[] GetEntityIds(IReadOnlyCollection<Guid> ids, bool validate = true)`
- `SourceKnownEntityId?[] GetEntityIds(IReadOnlyCollection<Guid?> ids, bool validate = true)`
- `SourceKnownEntityId[] GetEntityIds<TOtherEntity>(IReadOnlyCollection<Guid> ids)`
- `SourceKnownEntityId?[] GetEntityIds<TOtherEntity>(IReadOnlyCollection<Guid?> ids)`
- `IEnumerable<SourceKnownEntityId> GetEntityIdsAsEnumerable(...)`

#### Pagination
- `Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null)`
- `Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationResultInfo? resultInfo, long jumpTo = 1, int pageSize = -1, int maxSize = -1, PageSortDirection direction = PageSortDirection.None, long totalCount = -1, bool updateTotalCount = false)`
- `IAsyncEnumerable<PaginationResultModel<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null)`

### RepositorySettings<TEntity>
Configuration for repository query behavior.
- `IgnoreAutoIncludes`: Prevents EF Core from applying global includes.
- `AsNoTracking`: Disables change tracking for read-optimized queries.
- `Filters`: `IReadOnlyDictionary<string, Expression<Func<TEntity, bool>>>` for global query filters (e.g., TenantId, SoftDelete).
- Methods: `AddFilter`, `RemoveFilter`, `ClearFilters`.

### EntityCreatedFilter
Date-based filtering logic for pagination and queries.
- **Filter Types**: `After`, `Before`, `Between`, `Outside`.
- **Factory Methods**:
  - `EntityCreatedFilter.After(DateTimeOffset date, bool inclusive = true)`
  - `EntityCreatedFilter.Before(DateTimeOffset date, bool inclusive = true)`
  - `EntityCreatedFilter.Between(DateTimeOffset begin, DateTimeOffset end, bool inclusive = true)`
  - `EntityCreatedFilter.Outside(DateTimeOffset begin, DateTimeOffset end, bool inclusive = true)`

## Data Persistence Conventions

The framework uses `DrnContext<T>` as the foundation for EF Core data access, providing several automatic conventions.

### DrnContext Features
- **Auto-Discovery**: `IEntityTypeConfiguration<T>` implementations are automatically discovered and applied if they reside in the same assembly as the context and share its namespace (or a sub-namespace).
- **Schema Naming**: The default database schema is derived from the context class name converted to `snake_case`.
- **Automatic Identity**: Entities inheriting from `SourceKnownEntity` have their `Id` property automatically assigned by `IDrnSaveChangesInterceptor` during `SaveChangesAsync` for new records. Properties are initialized by `IDrnMaterializationInterceptor` when reading from DB.

### Advanced Database Configuration
The framework provides attributes for Npgsql-specific configuration directly on the `DrnContext` class.

- **NpgsqlDbContextOptionsAttribute**: Base attribute for context-specific database configuration.
  - **Overrideable Methods**:
    - `ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? sp)`: Configure Npgsql-specific options.
    - `ConfigureNpgsqlDataSource<TContext>(NpgsqlDataSourceBuilder builder, IServiceProvider sp)`: Configure the `NpgsqlDataSourceBuilder`.
    - `ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder builder, IServiceProvider? sp)`: Configure general EF Core `DbContextOptionsBuilder`.
    - `SeedAsync(IServiceProvider sp, IAppSettings settings)`: Provide custom data seeding logic.
- **NpgsqlPerformanceSettingsAttribute**: Abstract base attribute for declarative fine-tuning of Npgsql connection string parameters. Use concrete implementations like `DrnContextPerformanceDefaultsAttribute`.
    - `Multiplexing`: Enables/disables Npgsql multiplexing.
    - `MaxAutoPrepare` / `AutoPrepareMinUsages`: Configures statement preparation.
    - `MinPoolSize` / `MaxPoolSize`: Pool management.
    - `ReadBufferSize` / `WriteBufferSize`: Internal buffer sizes.
    - `CommandTimeout`: Default timeout for commands.
  - **Configuration Properties**:
    - `UsePrototypeMode`: (bool) Enables ephemeral, testcontainer-based local development.
    - `UsePrototypeModeWhenMigrationExists`: (bool) Enables prototype mode even if migrations exist.

> [!TIP]
> **Prototype Mode**: Set `UsePrototypeMode = true` on your configuration attribute to enable ephemeral, testcontainer-based local development. This automatically handles database creation and migrations for your context without requiring a manual PostgreSQL installation.
>
> **Global Activation**: Prototype mode is only active when following `DrnDevelopmentSettings` are enabled in your app settings (typically `appsettings.Development.json`):
> - `LaunchExternalDependencies = true`: Replaces the connection string with a Testcontainer instance.
> - `AutoMigrate = true`: Required for automatic schema initialization.
> - `Prototype = true`: Enables the fast prototyping logic that recreates the database on model changes.

```csharp
// Option A: Declarative Performance Settings (using concrete attribute)
[DrnContextPerformanceDefaults(MaxPoolSize = 100, CommandTimeout = 30)]
public class MyContext : DrnContext<MyContext> { ... }

// Option B: Custom Configuration Logic
public class MyConfigAttribute : NpgsqlDbContextOptionsAttribute {
    public override void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? sp)
        => builder.CommandTimeout(60);
}
```

### Model Composition Patterns
- **JSON Models**: Entities implementing `IEntityWithModel<TModel>` have their `Model` property automatically mapped to a `jsonb` column using EF Core's JSON support (`builder.OwnsOne(...).ToJson()`).
- **Audit Properties**: `CreatedAt` and `ModifiedAt` are automatically tracked and have standardized column order configurations.

### Configuration Best Practices
- **Isolation**: Place `IEntityTypeConfiguration` classes in a `Configurations` folder within the specific Context's directory in the infrastructure layer (e.g., `Infra/Marketing/Configurations`).
- **Repositories**: Place implementation classes in a `Repositories` folder within the specific Context's directory (e.g., `Infra/Marketing/Repositories`).
- **Explicit Key Handling**: While the base context handles standard keys, use configurations for unique indexes, relationships, and property-specific constraints (e.g., `HasMaxLength`).

## Repository Implementation

While `ISourceKnownRepository<TEntity>` defines the contract, the framework provides `SourceKnownRepository<TContext, TEntity>` as the standard base class for EF Core implementations.

### SourceKnownRepository (Base Class)
Concrete repositories should inherit from this base to leverage built-in query handling and auditing.

#### Protected Properties
- `Context`: Direct access to the underlying `TContext` (DbContext).
- `Entities`: Direct access to the `DbSet<TEntity>`.
- `Utils`: Access to `IEntityUtils` for identity and cancellation logic.
- `ScopedLog`: Integrated logger for performance measuring and tracing.

#### Query Composition Methods
Base class methods for starting queries with standardized behavior:
- `EntitiesWithAppliedSettings()`: **Preferred entry point.** Returns `IQueryable<TEntity>` with `AsNoTracking`, `IgnoreAutoIncludes`, and global `Filters` applied based on the current `Settings`.
- `EntitiesWithAppliedBaseSettings()`: Applies only the global `Filters` and query tagging.

> [!TIP]
> Always use `EntitiesWithAppliedSettings()` when building custom queries (joins, complex filters) to ensure repository settings are respected.

### Overriding Standard Behavior

Override `EntitiesWithAppliedSettings` to change the behavior of all built-in retrieval methods. This is the recommended way to add global `Include` statements, especially for nested properties.

```csharp
protected override IQueryable<TEntity> EntitiesWithAppliedSettings(string? caller = null)
{
    return base.EntitiesWithAppliedSettings(caller)
        .Include(x => x.NestedProperty)
            .ThenInclude(n => n.SubProperty);
}
```

## Pagination Models

The framework provides structured models for cursor-based and offset-based pagination, optimized for API usage and model binding.

### PaginationRequest
Represents the input parameters for a paginated query. It is natively URL serializable for query string binding.

#### Factory Methods
- `PaginationRequest.Default`: Shorthand for simple one-page request.
- `PaginationRequest.DefaultWith(pageSize, maxSize, direction, totalCount, updateTotalCount)`: Full control over initial parameters.
- `PaginationRequest.From(resultInfo, jumpTo, pageSize, maxSize, direction, totalCount, updateTotalCount)`: Reconstructs a request from a previous results metadata, allowing for navigation (next/previous/jump).

#### Key Components & Dependencies
- `PageSize`: Holds logic for enforcing minimum and maximum record counts.
- `PageCursor`: Stores the `FirstId` and `LastId` from the current result set to enable key-set (cursor) pagination.
- `PaginationTotal`: Tracks the current state of total count calculation.

### PaginationResultInfo
Metadata returned alongside paginated items, enabling callers to construct subsequent navigation requests.

#### Structure & Dependencies
- Inherits from `PaginationResultBase`.
- Holds the original `PaginationRequest` that produced the result.
- **Navigation Metadata**: `HasNext`, `HasPrevious`, `FirstId`, `LastId`, and `ItemCount`.
- **Factory/Helpers**:
  - `RequestNextPage()`: Generates the specialized `PaginationRequest` for the next page.
  - `RequestPreviousPage()`: Generates the specialized `PaginationRequest` for the previous page.
  - `RequestPage(pageNumber)`: Navigates to a specific page using cursor boundaries when possible.

### PaginationResultModel<TModel>
Generic wrapper for a page of data.
- **Items**: `IReadOnlyList<TModel>` - The actual data set.
- **Info**: `PaginationResultInfo` - The navigation metadata.
- **Mapping**: `ToModel<TMapped>(mapper)` - Fluent utility to transform domain entities into DTOs while preserving pagination metadata.
- **Mapping Rule**: `PaginationResultModel` must always contain DTOs or response models when returned from an API. Mapping must be performed using `.ToModel(e => e.ToDto())` or a similar converter. 

## Entity Utilities

`IEntityUtils` (from `DRN.Framework.Utils`) provides scoped utilities for common domain operations:

### Available Utilities

| Property | Type | Purpose |
|----------|------|----------|
| `Id` | `ISourceKnownIdUtils` | Generate internal long IDs |
| `EntityId` | `ISourceKnownEntityIdUtils` | Generate/parse/validate external GUIDs |
| `Cancellation` | `ICancellationUtils` | Token management and merging |
| `Pagination` | `IPaginationUtils` | Pagination logic helpers |
| `DateTime` | `IDateTimeUtils` | Time-aware operations |
| `ScopedLog` | `IScopedLog` | Integrated logging |

### Usage in Repositories

```csharp
public class MyRepository(QAContext context, IEntityUtils utils) 
    : SourceKnownRepository<QAContext, MyEntity>(context, utils)
{
    // Utils automatically available via base class
    public async Task<MyEntity> GetByExternalIdAsync(Guid id)
    {
        var entityId = Utils.EntityId.Validate<MyEntity>(id);
        return await GetAsync(entityId);
    }
}
```

---

## Public API Integration Standards

To maintain consistency across the DRN ecosystem, follow these patterns when exposing domain entities via controllers.

### Entity Identification

**Critical Rules**:
- **Route Parameters**: Use `:guid` constraints for single entity actions (e.g., `[HttpGet("{id:guid}")]`)
- **DTO Mapping**: Map `AggregateRoot.EntityId` to the DTO's `Id` property (automatic if inheriting from `Dto`)
- **DTO Inheritance**: DTOs must derive from `Dto`
- **DTO Id Type**: DTOs must use `Guid` for identifiers, not `SourceKnownEntityId`
- **Mappers**: Implement DTO-to-Entity and Entity-to-DTO mappers as extension methods in a `static class` within the same file as the Entity definition

> [!IMPORTANT]
> **External Identity Rule**: Always use `Guid EntityId` (mapped as `Id` in DTOs) for all public-facing contracts, API route parameters, and external lookups. The internal `long Id` must never be exposed outside the infrastructure/domain boundaries.
>
> **Entity Exposure Prohibition**: Entities must **never** be exposed via public APIs (Controllers, Response Models). Always use DTOs or specialized response models. Entities are permitted in Razor Pages (Internal UI) when direct access is required for rendering logic.

### Pagination Patterns

> [!WARNING]
> **Strict Rule**: **Never enumerate all entities unless explicitly requested.** Always use pagination to protect system performance. The `GetAllAsync()` method should only be used when bounded by settings filters or for small, known-size collections.

**Best Practices**:
- **Preference**: Use `PaginateAsync` for all list endpoints
- **Model Binding**: Use `[FromQuery] PaginationRequest request` for default GET endpoints (natively URL serializable)
- **Controller Overloads**: Implement the following triad for comprehensive pagination support:
  1. `GetAsync([FromQuery] PaginationRequest request)`: Main entry point
  2. `PaginateWithQueryAsync([FromQuery] PaginationResultInfo? resultInfo, ...)`: Navigation support via query string
  3. `PaginateWithBodyAsync([FromBody] PaginationResultInfo? resultInfo, ...)`: Navigation support via request body (POST)

### Implementation Template

```csharp
// Controller
[ApiController]
[Route("api/[controller]")]
public class MyEntityController(ISourceKnownRepository<MyEntity> repository) : ControllerBase
{
    [HttpGet]
    public async Task<PaginationResultModel<MyDto>> GetAsync([FromQuery] PaginationRequest request)
    {
        var result = await repository.PaginateAsync(request);
        return result.ToModel(entity => entity.ToDto());
    }
    
    [HttpGet("{id:guid}")]
    public async Task<MyDto> GetByIdAsync(Guid id)
    {
        var entity = await repository.GetAsync(id);
        return entity.ToDto();
    }
}

// From entity and ToDTO Mapper methods (in same file as MyEntity) 
public static class MyEntityExtensions
{
    public static MyDto ToDto(this MyEntity entity) => new()
    {
        Id = entity.EntityId,  // External GUID
        Name = entity.Name,
        CreatedAt = entity.CreatedAt
    };
}
```

---

## Related Skills
- [drn-sharedkernel](../drn-sharedkernel/SKILL.md)
- [drn-entityframework](../drn-entityframework/SKILL.md)
- [drn-hosting](../drn-hosting/SKILL.md)
- [drn-utils](../drn-utils/SKILL.md)