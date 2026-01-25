---
name: drn-domain-design
description: Domain-Driven Design patterns and implementations - Entity design with Source-Known IDs, EntityType attributes, repository patterns, domain event handling, and EF Core entity configuration. Critical for implementing domain models and data access layers. Keywords: ddd, entity-design, repository-pattern, source-known-id, entity-type, domain-events, ef-core-configuration, fluent-api, entity-framework, domain-modeling
---

# Domain Design Skill (DRN.Framework)

This skill provides standards and patterns for domain-driven design within the DRN.Framework, emphasizing type-safe identity, consistent entity life-cycle, and standard repository contracts.

> [!TIP]
> **Design Preference**: Prefer **Attribute-based design** over Fluent API when available. Use Fluent API only for **complex definitions** that cannot be elegantly expressed with attributes (e.g., composite keys, complex many-to-many relationships, or conditional mapping). This keeps standard configuration co-located with implementation while reserving the power of Fluent API for exceptional cases.

## Identity System

The framework uses a composite identifier system to balance DB performance (long IDs) with external security (GUIDs) and type safety.

> [!IMPORTANT]
> **External Identity Rule**: Always use `Guid EntityId` (mapped as `Id` in DTOs) for all public-facing contracts, API route parameters, and external lookups. The internal `long Id` must never be exposed outside the infrastructure/domain boundaries. DTOs must expose `Guid Id` (not `SourceKnownEntityId`) to ensure safe serialization.

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
Identity reconstruction is handled via delegates in `SourceKnownEntity`:
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
- **Automatic Identity**: Entities inheriting from `SourceKnownEntity` have their `Id` property automatically configured with `SourceKnownIdValueGenerator`.

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

## Entity Utilities

`EntityUtils` (found in `DRN.Framework.Utils/Entity`) provides a scoped context for common domain operations:
- `Id`: Identity utilities.
- `EntityId`: Public ID utilities.
- `Cancellation`: Token management.
- `Pagination`: Logic helpers.
- `DateTime`: Time-aware operations.
- `ScopedLog`: Integrated logging.

## Public API Integration Standards

To maintain consistency across the DRN ecosystem, follow these patterns when exposing domain entities via controllers:

### Entity Identification
- **Route Parameters**: Use `:guid` constraints for single entity actions (e.g., `[HttpGet("{id:guid}")]`).
- **DTO Mapping**: Map `AggregateRoot.EntityId` to the DTO's `Id` property.
- **DTO Id Type**: DTOs must use `Guid` for identifiers, not `SourceKnownEntityId`.
- **Mappers**: Implement DTO-to-Entity and Entity-to-DTO mappers as extension methods in a `static class` within the same file as the Entity definition.

### Pagination Patterns
- **Strict Rule**: **Never enumerate all entities unless explicitly requested.** Always use pagination instead to protect system performance.
- **Preference**: Use `PaginateAsync` for all list endpoints.
- **Model Binding**: Use `[FromQuery] PaginationRequest request` for default GET endpoints; it is natively URL serializable.
- **Controller Overloads**: Implement the following triad for comprehensive pagination support:
  1. `GetAsync([FromQuery] PaginationRequest request)`: Main entry point.
  2. `PaginateWithQueryAsync([FromQuery] PaginationResultInfo? resultInfo, ...)`: Navigation support via query string.
  3. `PaginateWithBodyAsync([FromBody] PaginationResultInfo? resultInfo, ...)`: Navigation support via request body (POST).

### Implementation Template
```csharp
[HttpGet]
public async Task<PaginationResultModel<MyDto>> GetAsync([FromQuery] PaginationRequest request)
{
    var result = await repository.PaginateAsync(request);
    return result.ToModel(entity => entity.ToDto());
}
```

## Related Skills
- [drn-sharedkernel](../drn-sharedkernel/SKILL.md)
- [drn-entityframework](../drn-entityframework/SKILL.md)
- [drn-hosting](../drn-hosting/SKILL.md)
- [drn-utils](../drn-utils/SKILL.md)