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

## TL;DR

- **Convention-Based DbContext** - `DrnContext<T>` supports attribute-based registration, configuration discovery, and migration management
- **Source Known persistence** - ID generation, materialization, validation, and secure/plain conversion hooks
- **Auto-Tracking** - Automatic `CreatedAt`/`ModifiedAt` timestamps and lifecycle hooks
- **Prototype Mode** - Auto-recreate a disposable Development database through startup validation
- **Repository Base** - `SourceKnownRepository<TContext, TEntity>` with pagination and validation

> [!WARNING]
> **Upcoming Features (v1.0.0)**: The following features will be available after DRN.Nexus integration is completed:
> - Auto-Migration in Production
> - Domain Event Publishing

## Table of Contents

- [QuickStart: Beginner](#quickstart-beginner)
- [QuickStart: Advanced](#quickstart-advanced)
- [Identity System](#identity-system)
- [DrnContext](#drncontext)
- [Context-Specific Migrations](#context-specific-migrations)
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
// 1. Define your entity inheriting from AggregateRoot (which derives from SourceKnownEntity)
[EntityType(1)] // Unique byte per entity type
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

    public DbSet<User> Users { get; set; }
}

// 3. Use in your service - IDs are generated before save
public class UserService(AppContext context)
{
    public async Task CreateUserAsync(string username)
    {
        context.Users.Add(new User { Username = username }); 
        await context.SaveChangesAsync(); // ID generated during SaveChangesAsync
    }
}
```

Register the assembly containing the context during application startup:

```csharp
builder.Services.AddServicesWithAttributes(typeof(AppContext).Assembly);
```

`DRN.Framework.Hosting` runs startup validation, migration, and seeding automatically. Standalone hosts must integrate the framework startup validation lifecycle explicitly.

## QuickStart: Advanced

Repository pattern with pagination and filtering:

Place public DTOs in the consuming application's `*.Contract` project:

```csharp
public sealed class UserDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Username { get; init; } = string.Empty;
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
        // EntitiesWithAppliedSettings() applies AsNoTracking, Filters, etc.
        return await EntitiesWithAppliedSettings()
            .Where(u => u.IsActive)
            .ToArrayAsync();
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

---

## Identity System

The framework uses a database-optimized internal identifier and exposes `Guid EntityId` for external use. `SourceKnownEntityId` supports domain and repository identity operations; do not expose it in public contracts.

> [!IMPORTANT]
> **External Identity Rule**: Always use `Guid EntityId` (mapped as `Id` in DTOs) for public contracts, API route parameters, and external lookups. Internal numeric IDs must never be exposed outside domain and infrastructure boundaries.

**Why Two IDs?**
- **Performance**: The internal numeric ID provides efficient database indexing and joins.
- **External identity**: Secure external IDs reduce predictability but do not replace authorization or rate limiting.
- **Type Safety**: Entity IDs are validated against the expected entity type.

## DrnContext

`DrnContext` is the foundational `DbContext` implementation that integrates with the DRN Framework ecosystem.

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

### Features

*   **Attribute-Based Registration**: Register the context assembly with `AddServicesWithAttributes`; the framework then registers discovered contexts and their conventions.
*   **Convention-Based Configuration**:
    *   Context name defines the connection string key (e.g., `QAContext` → `ConnectionStrings:QAContext`).
    *   Automatically applies `IEntityTypeConfiguration` from the context's assembly when its namespace matches the context namespace or a child namespace.
    *   Schema naming derived from context name in `snake_case`.
*   **Audit Support**: Automatically manages `CreatedAt`/`ModifiedAt` and invokes Source-Known lifecycle hooks. Domain-event publication is not included.
*   **Integration Testing**: Native support for `DRN.Framework.Testing`'s `ContainerContext` for isolated Postgres container tests.

## Context-Specific Migrations

DRN Framework simplifies multi-context projects by automatically managing migration locations via `DrnMigrationsScaffolder`.

- **Clean Project Structure**: Keeps migrations separated logically by context, preventing clutter in the project root.

> [!TIP]
> **Migration Startup Project**: When adding or applying migrations, use the project containing the `DrnContext` as the startup project (e.g., `dotnet ef migrations add Name --project Sample.Infra --startup-project Sample.Infra`). Place the context in a namespace rooted at its assembly name so generated migrations use the expected location.

## Identity Naming Conventions

When using `DrnContextIdentity`, the framework automatically applies clean `snake_case` naming to standard ASP.NET Core Identity tables.

| Original Table | DRN Table Name |
| :--- | :--- |
| `AspNetUsers` | `users` |
| `AspNetUserLogins` | `user_logins` |
| `AspNetUserClaims` | `user_claims` |
| `AspNetRoles` | `roles` |
| `AspNetUserRoles` | `user_roles` |
| `AspNetRoleClaims` | `role_claims` |
| `AspNetUserTokens` | `user_tokens` |

This ensures that your identity schema feels at home with the rest of your `snake_case` domain tables.

### Entity ID Generation

Entities inheriting from `SourceKnownEntity` have their IDs automatically generated before being persisted to the database:

*   **Generation**: Assigns a Source-Known ID during `SaveChangesAsync`.
*   **External Identity**: Exposes `Guid EntityId` for public contracts and lookups.
*   **Requirement**: Every entity must have a unique `[EntityType(n)]` attribute.

```csharp
[EntityType(1)]
public class User : AggregateRoot
{
    // Id is generated during SaveChangesAsync
    public string Username { get; set; }
}
```

### Startup Validation

When the framework startup validation lifecycle runs, registered contexts are validated:

*   **Context Validation**: Validates that registered contexts can be resolved.
*   **Entity Type Check**: Ensures Source-Known entity types have unique `[EntityType]` attributes.
*   **Auto-Migration & Seeding**:
    *   Applies pending migrations when automatic migration is enabled for the current environment.
    *   Runs `SeedAsync` after this package applies migrations or creates or recreates a prototype database. Seed implementations must be idempotent. See [EF Core Data Seeding Guidance](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding).

### Example

```csharp
public class QAContext : DrnContext<QAContext>
{
    public QAContext(DbContextOptions<QAContext> options) : base(options) { }
    public QAContext() : base(null) { }  // Required for migrations

    public DbSet<User> Users { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<Answer> Answers { get; set; }
}
```

## SourceKnownRepository

`SourceKnownRepository<TContext, TEntity>` is the EF Core implementation of `SharedKernel.ISourceKnownRepository`. It provides a data access layer with built-in performance and consistency checks.

### IEntityUtils

Repositories require [`IEntityUtils`](https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Utils/Entity/EntityUtils.cs) (defined in `DRN.Framework.Utils`) for core domain operations:

```csharp
public class UserRepository(QAContext context, IEntityUtils utils) 
    : SourceKnownRepository<QAContext, User>(context, utils), IUserRepository
{
    // Custom query methods...
}
```

**IEntityUtils provides:**
- **Id**: Numeric identity generation and parsing utilities
- **EntityId**: GUID ↔ SourceKnownEntityId conversion (including `ToSecure` / `ToPlain`)
- **Cancellation**: Explicit root cancel-all plus isolated repository scopes and opt-in shared groups
- **Pagination**: Pagination logic helpers
- **DateTime**: Time-aware operations
- **ScopedLog**: Integrated performance logging

### Repository Cancellation

Repository cancellation scope is configured via `Settings.ScopeKey`:

- When `Settings.ScopeKey` is `null` (default), repository cancellation uses `Utils.Cancellation.Root`. `CancelChanges()` cancels `Root`, affecting all scope-wide operations.
- When `Settings.ScopeKey` is set to a `CancellationScopeKey`, operations use that child scope:
  - `CancellationToken` returns the child scope token.
  - `CancelWhen(token)` links a lifetime token to the repository group.
  - `CancelChanges()` cancels only repositories sharing that scope key.

```csharp
repository.Settings.ScopeKey = CancellationScopeKey.For<UserRepository>("shared-writes");
```

Names are optional, case-sensitive developer-defined constants limited to 128 characters. Use one only when a type owns multiple intentional groups.

Never derive keys from request data, user input, instance IDs, or operation IDs. For operation-only cancellation, link the operation token locally instead of adding it to the repository group. See [Scoped Cancellation](https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Utils/README.md#scoped-cancellation) for key and lifetime rules.

### RepositorySettings

Configure repository behavior via the `Settings` property:

```csharp
public class RepositorySettings<TEntity>
{
    public bool AsNoTracking { get; set; }           // Disable change tracking
    public bool IgnoreAutoIncludes { get; set; }     // Prevent auto-loading navigations
    public CancellationScopeKey? ScopeKey { get; set; } // Configure child cancellation scope
    public IReadOnlyDictionary<string, Expression<Func<TEntity, bool>>> Filters { get; }
}
```

**Configuration Examples:**

```csharp
// Read-only queries (performance optimization)
repository.Settings.AsNoTracking = true;
repository.Settings.IgnoreAutoIncludes = true;

// Tenant filtering (applied to all queries)
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

Efficient cursor-based pagination using `PaginateAsync`:

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
var filter = EntityCreatedFilter.After(DateTime.UtcNow.AddDays(-7));
var recentUsers = await repository.PaginateAsync(request, filter);

// Map to DTOs while preserving pagination
var dtoResult = result.ToModel(user => new UserDto(user)
{
    Username = user.Username
});
```

### Query Composition

For custom queries, use protected methods that respect repository settings:

```csharp
public class UserRepository(QAContext context, IEntityUtils utils) 
    : SourceKnownRepository<QAContext, User>(context, utils)
{
    public async Task<User[]> GetActiveUsersAsync()
    {
        // EntitiesWithAppliedSettings() applies AsNoTracking, IgnoreAutoIncludes, and Filters
        var query = EntitiesWithAppliedSettings()
            .Where(u => u.IsActive);
            
        return await query.ToArrayAsync();
    }
    
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

```csharp
public class UserRepository(QAContext context, IEntityUtils utils) 
    : SourceKnownRepository<QAContext, User>(context, utils), IUserRepository
{
    protected override IQueryable<User> EntitiesWithAppliedSettings(string? caller = null)
    {
        // Start with base settings (AsNoTracking, Filters, etc.)
        return base.EntitiesWithAppliedSettings(caller)
            .Include(u => u.Posts)
                .ThenInclude(p => p.Comments)
            .Include(u => u.Profile);
    }
}
```

The override applies the specified navigation loading while preserving the base query settings.

### Validation

The repository validates `SourceKnownEntityId` entity types before query execution by default:

```csharp
// This will throw ValidationException if the ID's EntityType doesn't match User
var userId = repository.GetEntityId(someGuid, validate: true);
var user = await repository.GetAsync(userId);

// Validate multiple IDs
var userIds = repository.GetEntityIds(guidList, validate: true);
var users = await repository.GetAsync(userIds);
```

### Secure ↔ Plain Conversion

Repositories expose idempotent conversion between encrypted and plaintext entity IDs:

```csharp
var secureId = repository.ToSecure(entityId);
var plainId = repository.ToPlain(entityId);
```

---

## Entity Configuration

The framework supports both attribute-based and Fluent API configuration.

### Attribute-Based Configuration (Preferred)

Use attributes for simple, standard configurations:

```csharp
[EntityType(1)]
[Table("users")]
[Index(nameof(Username), IsUnique = true)]
public class User : SourceKnownEntity
{
    [MaxLength(100)]
    [Required]
    public string Username { get; set; }
    
    [MaxLength(255)]
    public string Email { get; set; }
    
    public bool IsActive { get; set; } = true;
}
```

### Fluent API Configuration (Complex Cases)

Use `IEntityTypeConfiguration` for complex relationships and conditional mapping:

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

> [!TIP]
> **Design Preference**: Prefer attribute-based design over Fluent API when available. Use Fluent API only for complex definitions that cannot be elegantly expressed with attributes (e.g., composite keys, complex many-to-many relationships, or conditional mapping).

### Auto-Discovery

Configurations are automatically discovered and applied if they:
- Reside in the same assembly as the context
- Share the context's namespace (or a sub-namespace)

```
Sample.Infra/
├── QAContext.cs                    # Namespace: Sample.Infra
├── Configurations/
│   ├── UserConfiguration.cs        # Namespace: Sample.Infra.Configurations ✓
│   └── QuestionConfiguration.cs    # Namespace: Sample.Infra.Configurations ✓
```

### JSON Models

Entities implementing `IEntityWithModel<TModel>` have their `Model` property automatically mapped to a `jsonb` column:

```csharp
public class Question : AggregateRoot<QuestionModel>
{
    // Model property is automatically configured as jsonb
}

public class QuestionModel
{
    public string Title { get; set; }
    public string Body { get; set; }
    public List<string> Tags { get; set; }
}
```

## Attributes & Configuration

### DrnContextServiceRegistrationAttribute

Participates in attribute-based registration and lifecycle management for your `DbContext`.

**Features:**
- Registers the context when its assembly is scanned
- During framework startup validation:
    - Validates entity type uniqueness
    - Applies pending migrations if configured
    - Runs seed data after automatic migrations or prototype database creation/recreation

### DrnContextDefaultsAttribute

Provides framework defaults for Npgsql and EF Core:

**Npgsql Defaults:**
- Query splitting behavior: `SplitQuery`
- Migrations assembly: Context's assembly
- Migrations history table: `{context_name}_history` in `__entity_migrations` schema
- PostgreSQL version: 18.4
- **Database History**: Automatically placed in the `__entity_migrations` schema (e.g., `__entity_migrations.mycontext_history`) to keep the public schema focused on domain data.

**Data Source Defaults:**
- Parameter logging: Disabled
- JSON options: Framework's `JsonConventions.DefaultOptions`
- Application name: `{AppName}_{ContextName}`

**DbContext Defaults:**
- Snake case naming convention
- Warning-level logging to `IScopedLog`

### DrnContextPerformanceDefaultsAttribute

Provides default performance settings for Npgsql:

```csharp
[DrnContextPerformanceDefaults(
    maxAutoPrepare: 200,
    autoPrepareMinUsages: 5,
    minPoolSize: 1,
    maxPoolSize: 15,
    readBufferSize: 8192,
    writeBufferSize: 8192,
    commandTimeout: 30
)]
public class MyContext : DrnContext<MyContext> { }
```

### NpgsqlDbContextOptionsAttribute

Base attribute for custom database configuration. Override methods to customize behavior:

```csharp
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

public class MyContextOptions : NpgsqlDbContextOptionsAttribute
{
    public override void ConfigureNpgsqlOptions<TContext>(
        NpgsqlDbContextOptionsBuilder builder, 
        IServiceProvider? serviceProvider)
    {
        // Configure Npgsql-specific options
        builder.CommandTimeout(60);
        builder.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
    }

    public override void ConfigureNpgsqlDataSource<TContext>(
        NpgsqlDataSourceBuilder builder,
        IServiceProvider serviceProvider)
    {
        // Configure Npgsql data-source features here.
    }

    public override void ConfigureDbContextOptions<TContext>(
        DbContextOptionsBuilder builder,
        IServiceProvider? serviceProvider)
    {
        base.ConfigureDbContextOptions<TContext>(builder, serviceProvider);
        // Configure general EF Core options here.
    }

    public override async Task SeedAsync(
        IServiceProvider serviceProvider, 
        IAppSettings appSettings)
    {
        // Seed after automatic migration or prototype database creation/recreation
        var context = serviceProvider.GetRequiredService<MyDbContext>();
        if (!await context.Users.AnyAsync())
        {
            context.Users.Add(new User { Username = "admin" });
            await context.SaveChangesAsync();
        }
    }
}
```

**Usage:**

```csharp
[MyContextOptions(UsePrototypeMode = true)]
public class MyDbContext : DrnContext<MyDbContext> { }
```

### NpgsqlPerformanceSettingsAttribute

Abstract base for declarative performance tuning. Create custom attributes by inheriting:

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

During framework startup validation, **Prototype Mode** enables rapid development by automatically recreating the database when model changes are detected.

### What It Does

When prototype mode is active in Development and pending model changes are detected, it can automatically drop and recreate the configured Development database when all prototype conditions are satisfied.

**Benefit**: Eliminates temporary migrations during initial prototyping.

> [!CAUTION]
> Prototype mode deletes the configured database. Use it only with a disposable, isolated Development database. Staging auto-migration never enables prototype recreation.

### How to Enable

Prototype mode requires the following conditions:

1. **Attribute Configuration**: Set `UsePrototypeMode = true` on your `NpgsqlDbContextOptionsAttribute`

```csharp
[MyContextOptions(UsePrototypeMode = true)]
public class MyDbContext : DrnContext<MyDbContext> { }
```

2. **Development Settings**: Configure in `appsettings.Development.json`

```json
{
  "DrnDevelopmentSettings": {
    "AutoMigrateDevelopment": true,
    "Prototype": true
  }
}
```

3. **Migration Workflow**: The application must run in Development with `AutoMigrateDevelopment = true`

### When Database Recreates

The database is recreated **only** when:
- Pending model changes exist
- `UsePrototypeMode = true` on the attribute
- `DrnDevelopmentSettings.Prototype = true` in configuration
- The application runs in Development with `AutoMigrateDevelopment = true`
- No migrations have been applied, or applied migrations exist and `UsePrototypeModeWhenMigrationExists = true`

> [!TIP]
> If `DrnDevelopmentSettings.Prototype` is `false`, the database is **never** recreated, even if `UsePrototypeMode` is enabled and model changes are detected. Prototype mode is intended for disposable Development databases; `LaunchExternalDependencies` is recommended for container isolation, but it is not itself a prototype-recreate condition.

### Prototype Mode with Applied Migrations

By default, prototype mode is disabled once migrations have been applied. Declared migrations that have not been applied do not block empty-database prototyping. To override the applied-migration guard:

```csharp
[MyContextOptions(
    UsePrototypeMode = true,
    UsePrototypeModeWhenMigrationExists = true
)]
public class MyDbContext : DrnContext<MyDbContext> { }
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

With automatic migration enabled, pending model changes require a migration unless all prototype conditions are satisfied. `SeedAsync` runs only after an automatic migration or prototype database creation/recreation; `DrnTestContext` helpers do not run it automatically.

> [!NOTE]
> Set `Environment` in base configuration, an environment variable, mounted configuration, or a command-line argument. An environment-specific settings file cannot select itself.

### Non-Development (Production/Staging)

**Explicit connection strings are required.** The framework calls `appSettings.GetRequiredConnectionString(contextName)`.

**Configuration Convention**: `ConnectionStrings:{ContextName}`

```json
{
  "ConnectionStrings": {
    "QAContext": "Host=prod-db.example.com;Port=5432;Database=qa_prod;User ID=qa_user;Password=***;..."
  }
}
```

> [!CAUTION]
> `postgres-password` and all `DrnContext_Dev*` settings are **ignored** in non-Development environments. Missing connection strings will throw `ConfigurationException`.

#### Staging

Staging uses the same `GetRequiredConnectionString` flow as Production — explicit connection strings are required.

| Setting | Default | Description |
|---------|---------|-------------|
| `IsStagingEnvironment` | `false` | `true` when `Environment=Staging` |
| `AutoMigrateStaging` | `false` | Enables automatic migrations in staging; does not enable prototype recreation |

**Example** `appsettings.Staging.json`, assuming Staging was selected by base configuration or an override:

```json
{
  "ConnectionStrings": {
    "QAContext": "Host=staging-db;Port=5432;Database=qa_staging;User ID=qa_user;Password=***;..."
  },
  "DrnDevelopmentSettings": {
    "AutoMigrateStaging": true
  }
}
```

> [!IMPORTANT]
> **AutoMigrateDevelopment vs AutoMigrateStaging**: `AutoMigrateDevelopment` defaults to `true` for frictionless local development. `AutoMigrateStaging` defaults to `false` for safe deployments — enable it explicitly for controlled staging migrations. For production-like staging, prefer CI/CD-managed migrations with rollback support.

---

### Local Debug with LaunchExternalDependencies

When `DrnDevelopmentSettings:LaunchExternalDependencies = true`, the framework uses Testcontainers to automatically start PostgreSQL.

**Setup**: Add a Debug-only `DRN.Framework.Testing` package reference and keep all DRN Framework package versions aligned:

```xml
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
    <PackageReference Include="DRN.Framework.Testing" Version="0.9.7" />
</ItemGroup>
```

**Implementation** (see [SampleProgramActions.cs](https://github.com/duranserkan/DRN-Project/blob/master/Sample.Hosted/SampleProgramActions.cs)):

```csharp
#if DEBUG
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

**Key Points**:
- `postgres-password` is **not used** - containers use `PostgresContainerSettings.DefaultPassword` (`"drn"`)
- Connection strings are automatically injected into configuration
- `Reuse = true` keeps the container running across application restarts

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
    image: postgres:18.4-alpine3.24
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

An explicit `ConnectionStrings:{ContextName}` value takes precedence in Development. Otherwise, `postgres-password` enables generation from the `DrnContext_Dev*` settings.

---

### DrnTestContext (Integration Tests)

For integration tests, `ContainerContext` manages Postgres containers automatically.

```csharp
[Theory]
[DataInline]
public async Task Integration_Test(DrnTestContext context)
{
    context.ServiceCollection.AddSampleInfraServices();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();
    
    var dbContext = context.GetRequiredService<QAContext>();
    // ... test code
}
```

**Key Points**:
- `DrnContext_Dev*` settings are **NOT used** - containers use [PostgresContainerSettings](https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Testing/Contexts/Postgres/PostgresContainerSettings.cs) defaults
- Connection strings from containers are automatically injected
- Migration and database-creation helpers do not run `SeedAsync`

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
| `DefaultVersion` | `"18.4-alpine3.24"` | Image tag |
| `Database` | `"drn"` | Container database |
| `Username` | `"drn"` | Container user |

> [!WARNING]
> **Prototype Mode Requirements**:
> 1. `NpgsqlDbContextOptionsAttribute.UsePrototypeMode = true` on context
> 2. `DrnDevelopmentSettings:Prototype = true`
> 3. The application runs in Development with `AutoMigrateDevelopment = true`
> 4. Pending model changes exist
> 5. No migrations have been applied, or applied migrations exist and `UsePrototypeModeWhenMigrationExists = true`
>
> If any condition is false, the database is **never** recreated.

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

```csharp
global using DRN.Framework.EntityFramework.Context;
global using Microsoft.EntityFrameworkCore;
global using DRN.Framework.Utils.DependencyInjection;
global using DRN.Framework.Utils.DependencyInjection.Attributes;
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
