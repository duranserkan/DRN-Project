---
name: drn-domain-design
description: Domain-Driven Design implementation patterns - Entity design with Source-Known IDs (SourceKnownEntity, AggregateRoot), repository contracts (ISourceKnownRepository), domain events, EF Core configuration, DTO mapping rules, and implementation templates. Essential for domain modeling and data access. Keywords: ddd, entity-design, aggregate-root, source-known-id, repository, domain-events, ef-core-configuration, dto-mapping, entity-type, identity
last-updated: 2026-02-15
difficulty: advanced
---

# Domain Design Patterns

> Entity design, repository implementation, and DDD patterns for DRN applications.

## When to Apply
- Creating domain entities & aggregate roots
- Implementing repositories
- Working with Source-Known IDs
- Configuring EF Core entity types
- Mapping entities to DTOs

---

## Entity Design

All entities inherit from `SourceKnownEntity`. See [drn-sharedkernel](../drn-sharedkernel/SKILL.md) for base class details and identity struct definitions.

### Creating an Entity

```csharp
[EntityType(1)]                          // Unique byte per entity
public class User : SourceKnownEntity    // or AggregateRoot
{
    private User() { }                   // EF constructor
    public User(string username, string email) : base()
    {
        Username = username;
        Email = email;
    }

    public string Username { get; private set; }
    public string Email { get; private set; }
    
    public void UpdateEmail(string newEmail)
    {
        Email = newEmail;
    }
}
```

### Key Rules
- `[EntityType(byte)]` is **required** — DrnContext validates at startup
- Private parameterless constructor for EF Core materialization
- Domain logic lives on the entity, not in services
- Never expose setters publicly — use methods with domain validation

### JSON Model Entities

For entities with rich document models, use `AggregateRoot<TModel>`:

```csharp
[EntityType(3)]
public class SystemSettings : AggregateRoot<SettingsModel> { }

public class SettingsModel
{
    public string Theme { get; set; }
    public int MaxRetries { get; set; }
}
// `.Model` is auto-mapped to jsonb column by DrnContext
```

---

## EF Core Configuration

> [!TIP]
> **Design Preference**: Prefer **Attribute-based configuration** over Fluent API when available. Use Fluent API only for **complex definitions** that cannot be elegantly expressed with attributes (e.g., composite keys, complex many-to-many relationships, or conditional mapping).

DrnContext auto-discovers `IEntityTypeConfiguration<T>` from the context's namespace:

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(100);
    }
}
```

### Conventions Applied by DrnContext
- Auto ID generation via `IDrnSaveChangesInterceptor`
- `EntityIdSource` initialization via `IDrnMaterializationInterceptor`
- `EntityId` (Guid) and `EntityIdSource` are **not mapped** to DB columns
- `IEntityWithModel<T>` auto-maps `.Model` to `jsonb`

> [!TIP]
> **Prototype Mode**: Set `UsePrototypeMode = true` on your configuration attribute to enable ephemeral, testcontainer-based local development. This auto-handles database creation and migrations without requiring manual PostgreSQL installation. See [drn-entityframework](../drn-entityframework/SKILL.md).

---

## Repository Implementation

Canonical contract in [drn-sharedkernel](../drn-sharedkernel/SKILL.md). Implementation in [drn-entityframework](../drn-entityframework/SKILL.md).

### IEntityUtils

`IEntityUtils` provides scoped utilities available in repositories via base class:

| Property | Type | Purpose |
|----------|------|---------|
| `Id` | `ISourceKnownIdUtils` | Generate internal `long` IDs |
| `EntityId` | `ISourceKnownEntityIdUtils` | Generate/parse/validate external GUIDs |
| `Cancellation` | `ICancellationUtils` | Token management and merging |
| `Pagination` | `IPaginationUtils` | Pagination logic helpers |
| `DateTime` | `IDateTimeUtils` | Time-aware operations |
| `ScopedLog` | `IScopedLog` | Integrated logging |

### Custom Repository with Behavior Override

```csharp
public interface IUserRepository : ISourceKnownRepository<User> { }

[Scoped<IUserRepository>]
public class UserRepository : SourceKnownRepository<QAContext, User>, IUserRepository
{
    protected override IQueryable<User> EntitiesWithAppliedSettings =>
        base.EntitiesWithAppliedSettings
            .Include(u => u.Profile)
            .ThenInclude(p => p.Address);
}
```

> [!TIP]
> Always use `EntitiesWithAppliedSettings()` when building custom queries (joins, complex filters) to ensure repository settings are respected.

### ID Validation at Repository Boundary

```csharp
// Validate external GUID matches entity type
var entityId = userRepository.GetEntityId(externalGuid);
var user = await userRepository.GetAsync(entityId);
```

---

## Contract Layer

> [!IMPORTANT]
> The `*.Contract` project is the **shared boundary** between layers. It holds DTOs, shared enums, and value models — anything consumed across Domain, Application, Infrastructure, or Presentation.

### Dependency Rule

- Contract depends **only** on `DRN.Framework.SharedKernel`
- Any project (including `*.Domain`) may reference `*.Contract`

### What Belongs in Contract

| Place Here | Never Place Here |
|------------|------------------|
| DTOs (`Dto` subclasses) | Entities |
| Shared enums (source of truth for DTOs & entities) | Repository interfaces |
| Value models (e.g., `TagValueModel`) | Domain services |
| Shared constants / display types | Business logic |

### Enum Placement Rule

Enums that are **shared between DTOs and entities** (e.g., `TagType`) must be defined in the Contract project as the single source of truth — unless there is a **strong objection** (security exposure, validation coupling, or maintainability risk).

Enums internal to the domain (e.g., `SampleEntityTypes` used only for `[EntityType]` attributes) remain in `*.Domain`.

---

## DTO Mapping Rules

> [!IMPORTANT]
> 1. All DTOs **must derive from `Dto` base class** and live in `*.Contract` project
> 2. Primary constructor accepts `SourceKnownEntity?` (base type, **not** concrete entity)
> 3. Entity-specific properties use `required` + `init` with default values
> 4. Public APIs must **never** return/accept Entities — always DTOs
> 5. Expose only `Guid` IDs, never `long Id` or `SourceKnownEntityId`
> 6. Keep DTOs concise — no pagination results, no unnecessary properties

### DTO Definition (in `*.Contract`)

```csharp
// Sample.Contract/QA/Categories/CategoryDto.cs
using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QA.Categories;

public class CategoryDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Name { get; init; } = string.Empty;
}
// Id, CreatedAt, ModifiedAt are auto-mapped by Dto base class
```

> The constructor takes `SourceKnownEntity?` — **not** the concrete entity type. This keeps the Contract layer independent of Domain. Entity-specific property mapping happens in the Domain layer via `ToDto()` instance methods.

### Entity-to-DTO Mapping (in `*.Domain`)

Map entity properties via `ToDto()` **instance method on the entity** using object initializers:

```csharp
// Sample.Domain/QA/Categories/Category.cs
using Sample.Contract.QA.Categories;

[EntityType((int)SampleEntityTypes.Category)]
public class Category : AggregateRoot
{
    // ... entity definition ...

    public CategoryDto ToDto() => new(this)
    {
        Name = Name
    };
}
```

The `new(this)` call passes the entity as `SourceKnownEntity?` to the base `Dto` constructor (maps `Id`, `CreatedAt`, `ModifiedAt`). The object initializer sets entity-specific `required` properties.

### DTO-to-Entity Mapping (in `*.Domain`)

Since Contract cannot reference Domain, DTO-to-Entity mapping uses **extension methods** defined in the Domain project:

```csharp
// Sample.Domain/QA/Categories/CategoryMappingExtensions.cs
using Sample.Contract.QA.Categories;

namespace Sample.Domain.QA.Categories;

public static class CategoryMappingExtensions
{
    public static Category ToEntity(this CategoryDto dto) => new(dto.Name);
}
```

> [!TIP]
> Extension methods must call the entity's **domain constructor** — never bypass invariant enforcement.

### Mapper Convention Summary

| Direction | Default Pattern | When Entity is Crowded |
|-----------|----------------|------------------------|
| Entity → DTO | Instance method `ToDto()` on entity | Extract to extension method |
| DTO → Entity | Extension method `dto.ToEntity()` in Domain | Same |

> When the entity grows crowded with mapping logic, extract `ToDto()` into the same extension class (e.g., `CategoryMappingExtensions`) to keep the entity focused on domain behavior.

---

## API Integration

> [!WARNING]
> **Never enumerate all entities unless explicitly requested.** Always use pagination to protect system performance.

### Controller Pagination Triad

Implement these three endpoints for comprehensive pagination support:

```csharp
// 1. Main entry point (URL-serializable)
[HttpGet]
public async Task<PaginationResultModel<UserDto>> GetAsync([FromQuery] PaginationRequest request)
{
    var result = await _userRepository.PaginateAsync(request);
    return result.ToModel(user => user.ToDto());
}

// 2. Navigation via query string
[HttpGet("paginate")]
public async Task<PaginationResultModel<UserDto>> PaginateWithQueryAsync(
    [FromQuery] PaginationResultInfo? resultInfo, ...) { ... }

// 3. Navigation via request body (POST)
[HttpPost("paginate")]
public async Task<PaginationResultModel<UserDto>> PaginateWithBodyAsync(
    [FromBody] PaginationResultInfo? resultInfo, ...) { ... }

[HttpGet("{id:guid}")]
public async Task<UserDto> GetByIdAsync(Guid id)
{
    var entity = await _userRepository.GetAsync(id);
    return entity.ToDto();
}
```

> **Mapping Rule**: `PaginationResultModel` must always contain DTOs when returned from API. Use `.ToModel(entity => entity.ToDto())` to map.

### EntityCreatedFilter

Date-based filtering for pagination and queries:

```csharp
// Factory methods (all accept optional inclusive parameter, default true)
EntityCreatedFilter.After(DateTimeOffset date)
EntityCreatedFilter.Before(DateTimeOffset date)
EntityCreatedFilter.Between(DateTimeOffset begin, DateTimeOffset end)
EntityCreatedFilter.Outside(DateTimeOffset begin, DateTimeOffset end)

// Usage
var filter = EntityCreatedFilter.After(DateTimeOffset.UtcNow.AddDays(-7));
var result = await repository.PaginateAsync(request, filter);
```

---

## Implementation Template

New entity checklist:
1. Create entity class with `[EntityType(N)]` and private parameterless constructor
2. Create DTO in `*.Contract` project with `Dto` base class and `required` + `init` properties
3. Add `ToDto()` instance method on entity with object initializer
4. Add `IEntityTypeConfiguration<T>` in infrastructure layer
5. Add `DbSet<T>` to `DrnContext`
6. Create repository interface (`IXxxRepository : ISourceKnownRepository<T>`)
7. Implement repository with `[Scoped<IXxxRepository>]`
8. Add migration: `dotnet ef migrations add AddXxx --context MyContext`

---

## Related Skills

- [drn-sharedkernel.md](../drn-sharedkernel/SKILL.md) - Entity base classes, repository contract, ID structs
- [drn-entityframework.md](../drn-entityframework/SKILL.md) - DrnContext, migrations, repository implementation
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Layer architecture
- [drn-utils.md](../drn-utils/SKILL.md) - Attribute-based DI