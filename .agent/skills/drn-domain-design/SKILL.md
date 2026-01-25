---
name: drn-domain-design
description: Core domain design patterns and implementations including identity management, entity bases, and repository contracts.
---

# Domain Design Skill (DRN.Framework)

This skill provides standards and patterns for domain-driven design within the DRN.Framework, emphasizing type-safe identity, consistent entity life-cycle, and standard repository contracts.

## Identity System

The framework uses a composite identifier system to balance DB performance (long IDs) with external security (GUIDs) and type safety.

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
- `Task<TEntity> GetAsync(Guid id / SourceKnownEntityId id)`
- `Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids / IReadOnlyCollection<SourceKnownEntityId> ids)`
- `Task<TEntity?> GetOrDefaultAsync(Guid id / SourceKnownEntityId id, bool validate = true)`

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

## Entity Utilities

`EntityUtils` (found in `DRN.Framework.Utils/Entity`) provides a scoped context for common domain operations:
- `Id`: Identity utilities.
- `EntityId`: Public ID utilities.
- `Cancellation`: Token management.
- `Pagination`: Logic helpers.
- `DateTime`: Time-aware operations.
- `ScopedLog`: Integrated logging.

## Related Skills
- [drn-sharedkernel](../drn-sharedkernel/SKILL.md)
- [drn-entityframework](../drn-entityframework/SKILL.md)
- [drn-utils](../drn-utils/SKILL.md)