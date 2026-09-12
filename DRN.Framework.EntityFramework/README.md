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

# DRN.Framework.EntityFramework

> Convention-based Entity Framework Core integration with automatic configuration, migrations, and Source-Known entity lifecycle support.

## Features

- `DrnContext<TContext>` provides attribute-based registration, model conventions, and design-time migration support.
- Source-Known entities receive internal IDs during tracking and external identity and lifecycle initialization before saving.
- `SourceKnownRepository<TContext, TEntity>` provides validated lookups, query filters, cancellation scopes, and cursor pagination.
- Startup validation can apply migrations in Development and Staging. Prototype mode can recreate a disposable Development database.

Production auto-migration and domain-event publishing are not implemented by this package.

## Table of Contents

- [QuickStart: Beginner](#quickstart-beginner)
- [QuickStart: Advanced](#quickstart-advanced)
- [Identity System](#identity-system)
- [DrnContext](#drncontext)
- [Context-Specific Migrations](#context-specific-migrations)
- [Seeding](#seeding)
- [Identity Naming Conventions](#identity-naming-conventions)
- [SourceKnownRepository](#sourceknownrepository)
- [Entity Configuration](#entity-configuration)
- [Attributes & Configuration](#attributes--configuration)
- [Prototype Mode](#prototype-mode)
- [Connection String Resolution by Environment](#connection-string-resolution-by-environment)
- [Configuration Settings Reference](#configuration-settings-reference)
- [Global Usings](#global-usings)
- [Related Packages](#related-packages)

---

## QuickStart: Beginner

Define a DbContext and entity with automatic ID generation:

```csharp
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

// 1. Define your application partition and entity
public readonly struct MyApp : IAppId
{
    public const byte Value = 1;
    public static byte AppId => Value;
}

[EntityType<MyApp>(1)] // Unique byte within MyApp partition
public class User : AggregateRoot
{
    public string Username { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

// 2. Create your context inheriting from DrnContext
public class AppContext : DrnContext<AppContext>
{
    public AppContext(DbContextOptions<AppContext> options) : base(options) { }
    public AppContext() : base(null) { } // Required for migrations

    public DbSet<User> Users => Set<User>();
}

// 3. Save a new entity
public class UserService(AppContext context)
{
    public async Task CreateUserAsync(string username)
    {
        var user = new User { Username = username };
        context.Users.Add(user); // Assigns the internal Id
        await context.SaveChangesAsync(); // Initializes external identity and lifecycle state
    }
}
```

Register the assembly containing the context during application startup and configure the matching `AppId` in `appsettings.json`:

```csharp
using DRN.Framework.Utils.DependencyInjection;

builder.Services.AddServicesWithAttributes(typeof(AppContext).Assembly);
```

```json
{
  "NexusAppSettings": {
    "AppId": 1
  }
}
```

`DRN.Framework.Hosting` runs startup validation automatically. Migration and seeding follow the environment settings described below. Standalone hosts must integrate the framework startup validation lifecycle explicitly.

Configure a connection using [Connection String Resolution by Environment](#connection-string-resolution-by-environment). The named key for this example is `ConnectionStrings:AppContext`. Use the additional imports in [Global Usings](#global-usings) for the examples below. Each example is an alternative or extension, not a second declaration to add to the same project.

## QuickStart: Advanced

Place public DTOs in the consuming application's `*.Contract` project. This example extends the beginner entity and context with a repository and controller:

```csharp
public sealed class UserDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Username { get; init; }
}

// Repository with custom query methods
public interface IUserRepository : ISourceKnownRepository<User> 
{
    Task<User[]> GetActiveUsersAsync();
}

[Scoped<IUserRepository>]
public class UserRepository(AppContext context, IEntityUtils utils) 
    : SourceKnownRepository<AppContext, User>(context, utils), IUserRepository
{
    public async Task<User[]> GetActiveUsersAsync()
    {
        return await EntitiesWithAppliedSettings()
            .Where(u => u.IsActive)
            .ToArrayAsync(CancellationToken);
    }
}

// Controller with pagination
[ApiController, Route("api/users")]
public class UserController(IUserRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<PaginationResultModel<UserDto>> GetAsync([FromQuery] PaginationRequest request)
    {
        var result = await repository.PaginateAsync(request);
        return result.ToModel(u => new UserDto(u) { Username = u.Username });
    }
    
    [HttpGet("{id:guid}")]
    public async Task<UserDto> GetByIdAsync(Guid id)
    {
        var user = await repository.GetAsync(id); // Validates ID automatically
        return new UserDto(user) { Username = user.Username };
    }
}
```

Scan the repository assembly with `AddServicesWithAttributes` too if it differs from the context assembly. Apply your application's authorization policy to these endpoints; ID validation does not grant access to a record.

---

## Identity System

The framework uses a database-optimized internal identifier and exposes `Guid EntityId` for external use. `SourceKnownEntityId` supports domain and repository identity operations; do not expose it in public contracts.

> [!IMPORTANT]
> **External Identity Rule**: Always use `Guid EntityId` (mapped as `Id` in DTOs) for public contracts, API route parameters, and external lookups. Internal numeric IDs must never be exposed outside domain and infrastructure boundaries.

The internal `long` is the database key used for indexes and joins. External IDs are validated against the expected entity type and application partition. Secure IDs reduce predictability but do not replace authorization or rate limiting.

## DrnContext

Derive from `DrnContext<TContext>` and provide both constructors shown in the beginner example. The options constructor is used by dependency injection; the public parameterless constructor supports `IDesignTimeDbContextFactory<TContext>`.

### Standard Attributes (Inherited)

Every `DrnContext` inherits registration, provider, and performance defaults. Customize database and performance settings with attributes derived from `NpgsqlDbContextOptionsAttribute` or `NpgsqlPerformanceSettingsAttribute`; service registration is inherited and should not be reapplied.

```csharp
// The base class defines these defaults:
[DrnContextServiceRegistration, DrnContextDefaults, DrnContextPerformanceDefaults]
public abstract class DrnContext<TContext> : DbContext, IDrnContext<TContext> 
    where TContext : DrnContext<TContext>, new()
{
    // ...
}
```

| Attribute | Description |
| --- | --- |
| `DrnContextServiceRegistration` | Context registration after assembly scanning; startup validation and migration management |
| `DrnContextDefaults` | Npgsql defaults, JSON configuration, logging setup |
| `DrnContextPerformanceDefaults` | Connection pooling, auto-prepare, command timeouts |

### Model Conventions

- The context's short name selects the connection string, such as `ConnectionStrings:QAContext`.
- Its name converted to `snake_case` is the default schema.
- `IEntityTypeConfiguration<T>` classes are discovered in the context assembly when their namespace equals the context namespace or is a child namespace.
- `ExtendedProperties` is an optional `jsonb` column. [JSON models](#json-models) use owned JSON mapping.
- `DomainEvent` and `IDomainEvent` are excluded from the model.

### Entity ID Generation

Entities inheriting from `SourceKnownEntity` receive internal IDs when EF begins tracking them. Save processing supplies a fallback if the internal ID is still zero and initializes external identity and lifecycle state before persistence:

| Stage | Behavior |
|---|---|
| Added to tracking | `SourceKnownIdValueGenerator` assigns a non-temporary internal `long` ID if it is zero |
| Saving an added entity | `DrnSaveChangesInterceptor` supplies a missing internal ID, initializes missing external identity and `EntityIdOps`, sets `ModifiedAt` to `CreatedAt`, and invokes the created hook |
| Saving a modified entity | Sets `ModifiedAt` to the current UTC time and invokes the modified hook |
| Saving a deleted entity | Invokes the deleted hook; this is not automatic soft deletion |
| Materializing a query result | `DrnMaterializationInterceptor` initializes `EntityIdSource` and `EntityIdOps`, enabling entity ID conversion operations |

`CreatedAt` is derived from the Source-Known ID. It is not a separately assigned creation timestamp. Lifecycle hooks can collect domain events; this package does not publish them.

For mapped inheritance, the key and shared properties belong to the EF hierarchy root. This supports table-per-hierarchy (TPH), table-per-type (TPT), and table-per-concrete-type (TPC) mappings. Derived entities inherit the key and ID generator. Each concrete entity still declares its own entity-type metadata. Abstract bases need no attribute, and ordinary CLR inheritance with an unmapped base retains the same conventions.

### Startup Validation

When the framework startup validation lifecycle runs, registered contexts are validated:

- Registered contexts must resolve from dependency injection.
- Concrete, non-private Source-Known entities require unique `(EntityType, AppId)` pairs. A different application partition may reuse the entity byte.
- A single context cannot contain multiple non-test `AppId` partitions. `NexusAppSettings:AppId` must match its partition or another registered host partition.
- Abstract and effectively private entities are excluded from model and assembly discovery. An entity is effectively private if it or any enclosing type is private, matching analyzer eligibility.
- Pending model changes fail validation even when auto-migration is disabled, unless the [prototype recreation conditions](#prototype-mode) are satisfied.
- Eligible automatic migration runs invoke [seeding](#seeding), including when no migrations remain.

## Context-Specific Migrations

`DrnMigrationsScaffolder` places migrations under the context's namespace relative to its assembly name, followed by `Migrations`. For example, a context in `Sample.Infra.QA` within the `Sample.Infra` assembly uses `QA/Migrations`. Contexts in the same namespace share that default location. An explicit output directory overrides the default location.

Use the project containing the context as the startup project, and keep the context namespace rooted at its assembly name. From the repository root:

```bash
dotnet ef migrations add AddUsers --context QAContext --project Sample.Infra --startup-project Sample.Infra
dotnet ef database update --context QAContext --project Sample.Infra --startup-project Sample.Infra -- "<connection-string>"
```

The design-time factory accepts the connection string as its first forwarded argument. Its options hooks receive a null service provider, and it does not install attribute seeding without application DI. Replace the connection placeholder with the target database connection.

## Seeding

Override `NpgsqlDbContextOptionsAttribute.SeedAsync` to seed a DI-configured context. See the [custom options example](#npgsqldbcontextoptionsattribute).

| Initialization path | Attribute seeding |
|---|---|
| Eligible automatic startup migration | Runs under EF's migration lock, including when no migrations remain; a later eligible startup can retry a failed seed |
| Explicit `Migrate` / `MigrateAsync` | Runs through EF callbacks for DI-configured contexts |
| Explicit `EnsureCreated` / `EnsureCreatedAsync`, including prototype creation | Runs through EF creation callbacks; does not provide the migration path's concurrency guarantee |
| Design-time options without an application service provider | Does not install attribute seeding |
| Test helpers | Seeds when the helper performs a migration or creation operation; shared migration helpers can skip already-migrated context types |

Custom EF callbacks run before attribute seeding. Synchronous initialization waits for `SeedAsync`. Each path prefers its matching custom callback and falls back to the other callback when only one is configured.

Reapplying context options preserves custom callbacks and replaces DRN wrappers with callbacks bound to the latest supplied provider. It does not duplicate attribute seeding. Reconfiguration without a provider restores only custom callbacks.

Seeds must tolerate repeated or partially completed runs and use the same scoped context for database work. The attribute hook has no cancellation-token parameter. Cancellation is checked before each attribute but cannot interrupt an attribute already running.

See [EF Core Data Seeding Guidance](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding) for the underlying EF callbacks.

## Identity Naming Conventions

`DrnContextIdentity<TContext, TUser>` supports ASP.NET Core Identity users derived from `IdentityUser`. It inherits registration and provider defaults, applies the model conventions, and renames these tables:

| Original Table | DRN Table Name |
|---|---|
| `AspNetUsers` | `users` |
| `AspNetUserLogins` | `user_logins` |
| `AspNetUserClaims` | `user_claims` |
| `AspNetRoles` | `roles` |
| `AspNetUserRoles` | `user_roles` |
| `AspNetRoleClaims` | `role_claims` |
| `AspNetUserTokens` | `user_tokens` |

It requires the same two constructors as `DrnContext`. Unlike `DrnContext`, it does not inherit `DrnContextPerformanceDefaults`.

## SourceKnownRepository

`SourceKnownRepository<TContext, TEntity>` implements `ISourceKnownRepository<TEntity>`. The context must implement `IDrnContext`, and the entity must derive from `AggregateRoot`.

Public CRUD, query, and pagination methods are virtual; protected pagination overloads are not.

### IEntityUtils

The repository constructor takes [`IEntityUtils`](https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Utils/Entity/EntityUtils.cs) from `DRN.Framework.Utils`:

| Member | Purpose |
|---|---|
| `Id` | Numeric identity generation and parsing |
| `EntityId` | `Guid` and `SourceKnownEntityId` conversion, including `ToSecure` and `ToPlain` |
| `Cancellation` | Root cancellation and child scopes |
| `Pagination` | Pagination helpers |
| `DateTime`, `UtcNow` | Entity date operations and the captured UTC time |
| `ScopedLog` | Operation timing and diagnostics |

### Repository Cancellation

Repository cancellation scope is configured via `Settings.ScopeKey`:

- When `Settings.ScopeKey` is `null` (default), repository cancellation uses `Utils.Cancellation.Root`. `CancelChanges()` cancels `Root`, affecting all scope-wide operations.
- When `Settings.ScopeKey` is set to a `CancellationScopeKey`, operations use that child scope:
  - `CancellationToken` returns the child scope token.
  - `CancelWhen(token)` links a lifetime token to the repository group.
  - `CancelChanges()` cancels only repositories sharing that scope key.

```csharp
repository.Settings.ScopeKey = CancellationScopeKey.For<UserRepository>("shared-writes");

// For one operation, use this token in the custom EF query.
using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(
    repository.CancellationToken, operationToken);
```

Names are optional, case-sensitive developer-defined constants limited to 128 characters. Use one only when a type owns multiple intentional groups.

Never derive keys from request data, user input, instance IDs, or operation IDs. For operation-only cancellation, link the operation token locally instead of adding it to the repository group. See [Scoped Cancellation](https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Utils/README.md#scoped-cancellation) for key and lifetime rules.

### RepositorySettings

Configure repository behavior through `Settings`:

| Property | Default | Effect |
|---|---|---|
| `AsNoTracking` | `false` | Disables tracking for retrieval queries |
| `IgnoreAutoIncludes` | `false` | Suppresses model-configured automatic includes for retrieval queries |
| `ScopeKey` | `null` | Selects a child cancellation scope; null uses the root |
| `Filters` | Empty | Read-only dictionary of named predicates; modify with `AddFilter`, `RemoveFilter`, and `ClearFilters` |

The tenant and soft-delete predicates below assume your entity defines `TenantId` and nullable `DeletedAt`. Neither property is supplied by `AggregateRoot`:

```csharp
// Read-only queries
repository.Settings.AsNoTracking = true;
repository.Settings.IgnoreAutoIncludes = true;

// Applies to repository reads and ID-based bulk deletes
repository.Settings.AddFilter("TenantId", 
    entity => entity.TenantId == currentTenantId);

// Soft delete filter
repository.Settings.AddFilter("NotDeleted", 
    entity => entity.DeletedAt == null);

// Remove a filter
repository.Settings.RemoveFilter("TenantId");

// Clear all filters
repository.Settings.ClearFilters();
```

### Pagination

Use `PaginateAsync` for cursor-based pagination:

```csharp
// Basic pagination
var request = PaginationRequest.DefaultWith(size: 20);
var result = await repository.PaginateAsync(request);

// Access results
foreach (var user in result.Items)
{
    Console.WriteLine(user.Username);
}

// Navigate to next page
if (result.Info.HasNext)
{
    var nextRequest = result.Info.RequestNextPage();
    var nextPage = await repository.PaginateAsync(nextRequest);
}

// Navigate to previous page
if (result.Info.HasPrevious)
{
    var prevRequest = result.Info.RequestPreviousPage();
    var prevPage = await repository.PaginateAsync(prevRequest);
}

// Filter by creation date
var filter = EntityCreatedFilter.After(DateTimeOffset.UtcNow.AddDays(-7));
var recentUsers = await repository.PaginateAsync(request, filter);

// Map to DTOs while preserving pagination
var dtoResult = result.ToModel(user => new UserDto(user)
{
    Username = user.Username
});
```

### Query Composition

Use `EntitiesWithAppliedSettings()` for custom retrieval queries, as in the advanced quickstart. The protected pagination overload accepts a composed query. This example assumes your `User` entity also defines a string `Role` property:

```csharp
public class UserRepository(AppContext context, IEntityUtils utils)
    : SourceKnownRepository<AppContext, User>(context, utils)
{
    public async Task<PaginationResultModel<User>> GetUsersByRoleAsync(
        string role, 
        PaginationRequest request)
    {
        var query = EntitiesWithAppliedSettings()
            .Where(u => u.Role == role);
            
        return await PaginateAsync(query, request);
    }
}
```

### Customizing Retrieval Queries

Override `EntitiesWithAppliedSettings` to customize repository retrieval queries, such as adding explicit navigation loading.

> [!IMPORTANT]
> Use `Settings.Filters` for constraints that must also apply to ID-based bulk deletes. An `EntitiesWithAppliedSettings` override customizes retrieval queries only.

This override assumes your model defines `User.Posts`, `Post.Comments`, and `User.Profile`. Add it to the repository from the advanced quickstart:

```csharp
protected override IQueryable<User> EntitiesWithAppliedSettings(string? caller = null)
{
    return base.EntitiesWithAppliedSettings(caller)
        .Include(u => u.Posts)
            .ThenInclude(p => p.Comments)
        .Include(u => u.Profile);
}
```

### Reads and Writes

| Operation | Behavior |
|---|---|
| `AnyAsync`, `AllAsync`, `CountAsync` | Evaluate retrieval queries with repository settings |
| `GetAsync(id)` | Returns one entity or throws `NotFoundException` |
| `GetOrDefaultAsync(id)` | Returns null when no matching entity exists; validates the ID by default |
| `GetAsync(ids)` | Returns matching entities; an empty input returns an empty array |
| `GetAllAsync()` | Loads every matching entity; use only for bounded result sets |
| `Add`, `Remove` | Change tracking state; require a later save |
| `CreateAsync(entities)`, `DeleteAsync(entities)` | Add or remove tracked entities, then save |
| `DeleteAsync(ids)` | Executes a database delete immediately with `Settings.Filters`, without fetching entities or invoking save interceptors |
| `SaveChangesAsync()` | Saves all pending changes in the context, including changes outside this repository |

Filters do not authorize tracked writes. Validate ownership and access before adding, modifying, or removing entities. ID-based bulk deletes bypass retrieval overrides and tracked lifecycle hooks. `CancelChanges()` cancels operations; it does not clear the change tracker or undo completed writes.

### Validation

Repositories validate the target entity's declared `(EntityType, AppId)` before querying, including secondary partitions. `GetEntityId<TOtherEntity>` uses that entity's declaration. `validate: false` skips validation; nullable inputs preserve null.

All `GetEntityId`, `GetEntityIds` and `GetEntityIdsAsEnumerable` overloads accept non-nullable `SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault`, including generic and nullable-ID forms on `ISourceKnownRepository<TEntity>`. ConfiguredDefault selects the ID utility's immutable `DefaultFormat`; explicit Secure, Plain or Auto overrides it. Helpers forward the format to parsing, then validate the parsed record's validity and identity. `validate: false` still parses with the selected format but returns the result without throwing for invalid IDs or mismatched identity. Undefined formats always throw, even for null IDs or empty batches. Enumerable helpers check the format at the call and parse IDs only during enumeration.

GUID-input `GetAsync`, `GetOrDefaultAsync` and `DeleteAsync` enforce DefaultFormat during parsing. Pass an explicit format to override it; accepting both GUID formats requires Auto. For deletion, use `DeleteAsync(ids, format)` with a GUID collection; the GUID params overload uses ConfiguredDefault. Parsed-record reads and deletes have no format parameter: they accept either representation and check stored validity and identity without reauthenticating the GUID. Pass only trusted internal records; untrusted GUIDs must go through parsing. Entity-input Add/Create/Delete operations also have no format parameter.

```csharp
// Throws ValidationException for an invalid ID or a mismatched entity type/partition
var userId = repository.GetEntityId(someGuid, validate: true);
var user = await repository.GetAsync(userId);

// Validate multiple IDs
var userIds = repository.GetEntityIds(guidList, validate: true);
var users = await repository.GetAsync(userIds);

// Accept both representations explicitly before querying
var mixedIds = repository.GetEntityIds(guidList, format: SourceKnownEntityIdFormat.Auto);
var mixedUsers = await repository.GetAsync(mixedIds);
// Or query GUIDs directly with the same explicit policy
var mixedUsersFromGuids = await repository.GetAsync(guidList, SourceKnownEntityIdFormat.Auto);
```

Rebuild binary consumers and update custom repository implementations, overrides and method-group bindings for the GUID helper/query signatures. Parsed-record query/delete APIs have no format parameter; the protected record-ID Filter is static. Conversion APIs retain their signatures.

### Secure ↔ Plain Conversion

Repositories expose idempotent conversion between encrypted and plaintext entity IDs:

```csharp
var secureId = repository.ToSecure(entityId);
var plainId = repository.ToPlain(secureId);
```

---

## Entity Configuration

The framework supports both attribute-based and Fluent API configuration.

### Attribute-Based Configuration (Preferred)

Use attributes for constraints and indexes that they can express. This is an alternative definition of the beginner `User` entity:

```csharp
[EntityType<MyApp>(1)]
[Table("users")]
[Index(nameof(Username), IsUnique = true)]
public class User : AggregateRoot
{
    [MaxLength(100)]
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
}
```

### Fluent API Configuration (Complex Cases)

Use `IEntityTypeConfiguration<T>` for relationships, owned types, or conditional mapping. This fragment assumes an application model with `Posts`, `Author`, `AuthorId`, `TenantId`, and an owned `Address` with `Street` and `City` properties:

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Complex relationships
        builder.HasMany(u => u.Posts)
               .WithOne(p => p.Author)
               .HasForeignKey(p => p.AuthorId)
               .OnDelete(DeleteBehavior.Cascade);
        
        // Composite indexes
        builder.HasIndex(u => new { u.TenantId, u.Username })
               .IsUnique();
        
        // Owned entities
        builder.OwnsOne(u => u.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(200);
            address.Property(a => a.City).HasMaxLength(100);
        });
    }
}
```

### Auto-Discovery

Configurations are automatically discovered and applied if they:

- Reside in the same assembly as the context
- Share the context's namespace (or a sub-namespace)

```text
Sample.Infra/
├── QAContext.cs                    # Namespace: Sample.Infra
├── Configurations/
│   ├── UserConfiguration.cs        # Namespace: Sample.Infra.Configurations ✓
│   └── QuestionConfiguration.cs    # Namespace: Sample.Infra.Configurations ✓
```

### JSON Models

Source-Known entities implementing `IEntityWithModel<TModel>` have their public `Model` property mapped as an owned JSON object with `OwnsOne(...).ToJson()`. PostgreSQL stores it as `jsonb`:

```csharp
[EntityType<MyApp>(2)]
public class Question : AggregateRoot<QuestionModel>
{
    public Question() => Model = new QuestionModel();
}

public class QuestionModel
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
}
```

## Attributes & Configuration

### DrnContextServiceRegistrationAttribute

Registers discovered contexts when their assembly is scanned and participates in [startup validation](#startup-validation). It is inherited from the context base class; do not reapply it.

### DrnContextDefaultsAttribute

| Setting | Default |
|---|---|
| Query splitting | `SplitQuery` |
| Migrations assembly | Context's assembly |
| Migration history | `__entity_migrations.{context_name}_history`, with the context name in `snake_case` |
| PostgreSQL compatibility version | 18.6 |
| Parameter logging | Disabled |
| JSON options | `JsonConventions.DefaultOptions` |
| Application name | Preserves an existing value; otherwise `{ApplicationName}_{ContextName}` with DI or `{ContextName}` without it |
| Table and column naming | `snake_case` |
| EF logging | Warning level and above to `IScopedLog`; console output when no scoped log is available |

### DrnContextPerformanceDefaultsAttribute

`DrnContext` inherits these Npgsql connection settings. To override them, derive a custom attribute from [NpgsqlPerformanceSettingsAttribute](#npgsqlperformancesettingsattribute).

| Constructor parameter | Default |
|---|---|
| `maxAutoPrepare` | `200` |
| `autoPrepareMinUsages` | `5` |
| `minPoolSize` | `1` |
| `maxPoolSize` | `15` |
| `readBufferSize` | `8192` bytes |
| `writeBufferSize` | `8192` bytes |
| `commandTimeout` | `30` seconds |

### NpgsqlDbContextOptionsAttribute

Derive an attribute to configure provider options, data-source options, general EF options, or seeding. Framework-defined attributes run before custom attributes. Options hooks must handle a null service provider at design time.

```csharp
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

public class MyContextOptions : NpgsqlDbContextOptionsAttribute
{
    public override void ConfigureNpgsqlOptions<TContext>(
        NpgsqlDbContextOptionsBuilder builder, 
        IServiceProvider? serviceProvider)
    {
        builder.CommandTimeout(60);
        builder.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
    }

    public override void ConfigureNpgsqlDataSource<TContext>(
        NpgsqlDataSourceBuilder builder,
        IServiceProvider? serviceProvider)
    {
        builder.ConnectionStringBuilder.ApplicationName = typeof(TContext).Name;
    }

    public override void ConfigureDbContextOptions<TContext>(
        DbContextOptionsBuilder builder,
        IServiceProvider? serviceProvider)
    {
        base.ConfigureDbContextOptions<TContext>(builder, serviceProvider);
        // The base method configures the EF warning used by prototype mode.
    }

    public override async Task SeedAsync(
        IServiceProvider serviceProvider, 
        IAppSettings appSettings)
    {
        var context = serviceProvider.GetRequiredService<MyDbContext>();
        if (!await context.Users.AnyAsync(user => user.Username == "example-user"))
        {
            context.Users.Add(new User { Username = "example-user" });
            await context.SaveChangesAsync();
        }
    }
}
```

Apply the attribute to a context with both required constructors:

```csharp
[MyContextOptions(UsePrototypeMode = true)]
public class MyDbContext : DrnContext<MyDbContext>
{
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }
    public MyDbContext() : base(null) { }

    public DbSet<User> Users => Set<User>();
}
```

The seed inserts an application record, not an authenticated account. See [Seeding](#seeding) for callback ordering, retries, concurrency, and cancellation limits.

### NpgsqlPerformanceSettingsAttribute

Create a custom performance attribute and apply `[HighThroughputSettings]` to your context. These example values are overrides, not workload recommendations:

```csharp
public class HighThroughputSettings : NpgsqlPerformanceSettingsAttribute
{
    public HighThroughputSettings() : base(
        maxAutoPrepare: 500,
        autoPrepareMinUsages: 3,
        minPoolSize: 10,
        maxPoolSize: 100,
        readBufferSize: 16384,
        writeBufferSize: 16384,
        commandTimeout: 60)
    {
    }
}
```

## Prototype Mode

Prototype mode creates or recreates a Development database from the current model during startup validation. It avoids temporary migrations while prototyping.

> [!CAUTION]
> Prototype mode deletes the configured database. Use it only with a disposable, isolated Development database. Staging auto-migration never enables prototype recreation.

### Conditions and Configuration

All conditions must hold:

1. The application runs in Development.
2. `DrnDevelopmentSettings:AutoMigrateDevelopment = true`.
3. Pending model changes exist.
4. A context options attribute has `UsePrototypeMode = true`.
5. `DrnDevelopmentSettings:Prototype = true`.
6. No migrations have been applied, or `UsePrototypeModeWhenMigrationExists = true` permits recreation despite applied migrations.

Apply `[MyContextOptions(UsePrototypeMode = true)]` as shown above, then configure `appsettings.Development.json`:

```json
{
  "DrnDevelopmentSettings": {
    "AutoMigrateDevelopment": true,
    "Prototype": true
  }
}
```

If any condition is false, this startup path does not recreate the database. `LaunchExternalDependencies` provides container isolation but is not a recreation condition. Isolate prototyping to one context and a disposable database; deletion affects the whole configured database.

For an existing database with tables, the workflow calls `EnsureDeletedAsync` before `EnsureCreatedAsync`. A missing or empty database is created without that deletion step. Creation invokes the [EF seed callback](#seeding).

Applied migrations are read from the target database independently of the local migration assembly. If migration files or the model snapshot are missing while the database still contains migration history, the database is treated as migrated and prototype recreation remains blocked unless `UsePrototypeModeWhenMigrationExists = true`.

### Prototype Mode with Applied Migrations

Declared migrations that have not been applied do not block prototyping. To override the applied-migration guard, replace the attribute on `MyDbContext` with:

```csharp
[MyContextOptions(
    UsePrototypeMode = true,
    UsePrototypeModeWhenMigrationExists = true
)]
// Keep the MyDbContext declaration and constructors shown above.
```

---

## Connection String Resolution by Environment

Connection strings vary by environment. The startup schema behavior below occurs when the framework startup validation lifecycle runs; `DRN.Framework.Hosting` invokes it automatically.

| Environment | Connection string | Startup schema behavior |
|---|---|---|
| Production | Explicit `ConnectionStrings:{ContextName}` | Never auto-migrates |
| Staging | Explicit `ConnectionStrings:{ContextName}` | Applies pending migrations only when `AutoMigrateStaging=true` |
| Development | Explicit named connection string, an injected Testcontainers connection, or generation from `postgres-password` and `DrnContext_Dev*` settings | Applies pending migrations when `AutoMigrateDevelopment=true`; prototype mode may recreate the database |
| `DrnTestContext` | Injected container connection | Migration and database-creation helpers perform only the requested operation |

Startup rejects pending model changes in every environment unless the prototype path handles them. Disabling automatic migration does not disable this validation. When automatic migration is enabled and no pending model changes exist, startup calls `MigrateAsync` even with zero pending migrations. See [Seeding](#seeding) for the callback behavior.

> [!NOTE]
> Set `Environment` in base configuration, an environment variable, mounted configuration, or a command-line argument. An environment-specific settings file cannot select itself.

### Non-Development (Production/Staging)

The framework calls `appSettings.GetRequiredConnectionString(contextName)`. Supply `ConnectionStrings:{ContextName}` through configuration; the password below is a placeholder:

```json
{
  "ConnectionStrings": {
    "QAContext": "Host=prod-db.example.com;Port=5432;Database=qa_prod;Username=qa_user;Password=<password>"
  }
}
```

> [!CAUTION]
> `postgres-password` and all `DrnContext_Dev*` settings are **ignored** in non-Development environments. Missing connection strings will throw `ConfigurationException`.

#### Staging

`IAppSettings.IsStagingEnvironment` is derived from `Environment=Staging`; it is not a separate configuration switch. `AutoMigrateStaging` defaults to `false` and enables migrations only, never prototype recreation.

**Example** `appsettings.Staging.json`, assuming Staging was selected by base configuration or an override:

```json
{
  "ConnectionStrings": {
    "QAContext": "Host=staging-db;Port=5432;Database=qa_staging;Username=qa_user;Password=<password>"
  },
  "DrnDevelopmentSettings": {
    "AutoMigrateStaging": true
  }
}
```

`AutoMigrateDevelopment` defaults to `true`; `AutoMigrateStaging` defaults to `false`. Enable staging migration only when the deployment should apply migrations at startup. Otherwise, apply them through your deployment process.

---

### Local Debug with LaunchExternalDependencies

In Development, call `LaunchExternalDependenciesAsync` with `DrnDevelopmentSettings:LaunchExternalDependencies = true` to start PostgreSQL through Testcontainers. The helper skips temporary applications and applications running inside `DrnTestContext`.

**Setup**: Add a Debug-only `DRN.Framework.Testing` package reference and keep all DRN Framework package versions aligned:

```xml
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
    <PackageReference Include="DRN.Framework.Testing" Version="0.10.0" />
</ItemGroup>
```

**Implementation** (see [SampleProgramActions.cs](https://github.com/duranserkan/DRN-Project/blob/master/Sample.Hosted/SampleProgramActions.cs)):

```csharp
#if DEBUG
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;

public class SampleProgramActions : DrnProgramActions
{
    public override async Task ApplicationBuilderCreatedAsync<TProgram>(
        TProgram program, WebApplicationBuilder builder,
        IAppSettings appSettings, IScopedLog scopedLog)
    {
        var launchOptions = new ExternalDependencyLaunchOptions
        {
            PostgresContainerSettings = new PostgresContainerSettings
            {
                Reuse = true,      // Keep container across restarts
                HostPort = 6432    // Avoid port conflicts
            }
        };
        await builder.LaunchExternalDependenciesAsync(scopedLog, appSettings, launchOptions);
    }
}
#endif
```

**Example** `appsettings.Development.json`, assuming Development was selected by base configuration or an override:

```json
{
  "DrnDevelopmentSettings": {
    "LaunchExternalDependencies": true,
    "AutoMigrateDevelopment": true,
    "Prototype": true
  }
}
```

Containers use `PostgresContainerSettings` rather than `postgres-password` or `DrnContext_Dev*`. The default password is `"drn"`. The launch helper injects named connection strings into configuration. `Reuse = true` keeps the container running across application restarts.

---

### Containerized Development (Docker Compose / Kubernetes)

For development with external database containers (Docker Compose, Kubernetes, Podman), use `postgres-password` to trigger auto-connection string generation.

**Docker Compose Example**:

```yaml
services:
  app:
    build: .
    environment:
      - Environment=Development
      - postgres-password=dev-password
      - DrnContext_DevHost=postgres
      - DrnDevelopmentSettings__AutoMigrateDevelopment=true
    depends_on:
      postgres:
        condition: service_healthy
      
  postgres:
    image: postgres:18.6-alpine3.24@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2
    environment:
      POSTGRES_USER: drn
      POSTGRES_PASSWORD: dev-password
      POSTGRES_DB: drn
      PGDATA: /data/postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/data/postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U drn -d drn"]
      interval: 5s
      timeout: 5s
      retries: 10

volumes:
  postgres-data:
```

**Kubernetes ConfigMap/Secret**:

Inject these values as environment variables from the ConfigMap and Secret.

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: app-config
data:
  Environment: "Development"
  DrnContext_DevHost: "postgres-service"
  DrnDevelopmentSettings__AutoMigrateDevelopment: "true"
---
apiVersion: v1
kind: Secret
metadata:
  name: app-secrets
stringData:
  postgres-password: "dev-password"
```

An explicit `ConnectionStrings:{ContextName}` value takes precedence in Development. Otherwise, `postgres-password` enables generation from the `DrnContext_Dev*` settings. Missing both a named connection and a password causes `ConfigurationException`.

---

### DrnTestContext (Integration Tests)

For integration tests, `ContainerContext` manages Postgres containers automatically. This method fragment uses the repository's `Sample.Infra` module; add your database assertions after resolving the context:

Use `DRN.Framework.Testing.Contexts`, `DRN.Framework.Testing.DataAttributes`, `Sample.Infra`, `Sample.Infra.QA`, and `Xunit` imports in the test file.

```csharp
[Theory]
[DataInline]
public async Task Integration_Test(DrnTestContext context)
{
    context.ServiceCollection.AddSampleInfraServices();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();
    
    var dbContext = context.GetRequiredService<QAContext>();
    // Add assertions for the operation under test.
}
```

Container connections are injected automatically. These tests use [PostgresContainerSettings](https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Testing/Contexts/Postgres/PostgresContainerSettings.cs), not `DrnContext_Dev*` settings. See [Seeding](#seeding) for helper callbacks and the shared migration skip behavior.

---

## Configuration Settings Reference

### Generated Development Connection Settings

These settings provide the Development fallback when no explicit named connection string or Testcontainers connection is available.

| Setting | Default | Purpose |
|---------|---------|---------|
| `DrnContext_DevHost` | `drn` | Database host |
| `DrnContext_DevPort` | `5432` | Database port |
| `DrnContext_DevUsername` | `drn` | Database username |
| `DrnContext_DevDatabase` | `drn` | Database name |
| `postgres-password` | *(none)* | Enables generated connection strings |

### Migration and Prototype Settings

These keys are under `DrnDevelopmentSettings`:

| Setting | Default | Purpose |
|---------|---------|---------|
| `AutoMigrateDevelopment` | `true` | Auto-migrate in Development |
| `AutoMigrateStaging` | `false` | Auto-migrate in Staging; migrations only, no prototype recreation |
| `Prototype` | `false` | Enables Development-only database recreation on model changes |
| `LaunchExternalDependencies` | `false` | Launches local PostgreSQL Testcontainers |

### Testcontainers Defaults

When using `LaunchExternalDependencies` or `ContainerContext`, these PostgreSQL values are used:

| Property | Default | Notes |
|----------|---------|-------|
| `DefaultPassword` | `"drn"` | Container password |
| `DefaultImage` | `"postgres"` | Docker image |
| `DefaultVersion` | `"18.6-alpine3.24"` | Image tag |
| `DefaultDigest` | `"sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2"` | Immutable image index digest |
| `Database` | `"drn"` | Container database |
| `Username` | `"drn"` | Container user |

The default image/tag pair is resolved with `DefaultDigest`. Custom image tags remain tag-based unless `Digest` is set explicitly.

The complete [prototype conditions](#prototype-mode) apply regardless of how the Development connection is supplied.

---

### DrnDevelopmentSettings Class

```csharp
public class DrnDevelopmentSettings
{
    public bool SkipValidation { get; init; }
    public bool TemporaryApplication { get; init; }
    public bool LaunchExternalDependencies { get; init; }
    public bool AutoMigrateDevelopment { get; init; } = true;
    public bool AutoMigrateStaging { get; init; } = false;
    public bool Prototype { get; init; }
    public bool BreakForUserUnhandledException { get; init; }
}
```


## Global Usings

Common imports for the entity, repository, configuration, and controller examples:

```csharp
global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
global using DRN.Framework.EntityFramework.Attributes;
global using DRN.Framework.EntityFramework.Context;
global using DRN.Framework.EntityFramework.Domain;
global using DRN.Framework.SharedKernel.Cancellation;
global using DRN.Framework.SharedKernel.Domain;
global using DRN.Framework.SharedKernel.Domain.Pagination;
global using DRN.Framework.SharedKernel.Domain.Repository;
global using DRN.Framework.Utils.DependencyInjection;
global using DRN.Framework.Utils.DependencyInjection.Attributes;
global using DRN.Framework.Utils.Entity;
global using DRN.Framework.Utils.Settings;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;
global using Microsoft.Extensions.DependencyInjection;
```

---

## Related Packages

- [DRN.Framework.SharedKernel](https://www.nuget.org/packages/DRN.Framework.SharedKernel/) - Domain primitives and exceptions
- [DRN.Framework.Utils](https://www.nuget.org/packages/DRN.Framework.Utils/) - Configuration and DI utilities
- [DRN.Framework.Hosting](https://www.nuget.org/packages/DRN.Framework.Hosting/) - Web application hosting
- [DRN.Framework.Testing](https://www.nuget.org/packages/DRN.Framework.Testing/) - Testing utilities

For persistence examples, see [Sample.Infra](https://github.com/duranserkan/DRN-Project/tree/master/Sample.Infra); for hosting setup, see [Sample.Hosted](https://github.com/duranserkan/DRN-Project/tree/master/Sample.Hosted).

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/.agent/rules/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
