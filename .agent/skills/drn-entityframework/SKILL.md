---
name: drn-entityframework
description: DRN.Framework.EntityFramework - DrnContext base class, automatic migration application, entity tracking with domain events, NpgsqlDbContextOptions for database configuration, and repository implementations. Essential for database operations, migrations, and data persistence. Keywords: drncontext, ef-core, migrations, database, postgresql, npgsql, repository-implementation, entity-tracking, domain-events, dbcontext-configuration, prototype-mode, testcontainers
---

# DRN.Framework.EntityFramework

> Convention-based Entity Framework Core integration with automatic configuration and migrations.

## When to Apply
- Creating new DbContexts
- Understanding DrnContext conventions
- Setting up database migrations
- Working with entity tracking and domain events
- Configuring development database connections

---

## Package Purpose

EntityFramework provides `DrnContext<T>` - a convention-based DbContext with automatic:
- Service registration
- Connection string resolution
- Entity configuration discovery
- Migration management
- Entity status tracking

---

## Directory Structure

```
DRN.Framework.EntityFramework/
├── Context/          # DrnContext, conventions, interceptors
├── Attributes/       # HasDrnContextServiceCollectionModule
├── Domain/           # Entity extensions
├── Extensions/       # EF extensions
└── DbContextCollection.cs
```

---

## DrnContext Pattern

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

### Key Features

| Feature | Description |
|---------|-------------|
| **Auto-registration** | Registered via `AddServicesWithAttributes()` |
| **Convention naming** | Connection string: `ConnectionStrings:QAContext` |
| **Config discovery** | Auto-applies `IEntityTypeConfiguration` from context namespace |
| **Entity tracking** | Auto-marks entities as Created/Modified/Deleted |
| **Design-time support** | Implements `IDesignTimeDbContextFactory` |

---

## Automatic Entity Tracking

Entities inheriting from `SourceKnownEntity` are automatically tracked:

```csharp

entity.MarkAsCreated();   // On Add, sets CreatedAt, ModifiedAt, adds created event
entity.MarkAsModified();  // On Update, sets ModifiedAt, adds modified event
entity.MarkAsDeleted();   // On Delete, sets deleted event
```

Domain events are collected and can be published after `SaveChangesAsync()`.

## Augmented Entity Behavior

`DrnContext` augments entities during `OnModelCreating` and runtime:

### 1. Interceptor-Based Identity
Entities inheriting from `SourceKnownEntity` have their identity managed via specialized interceptors:
- **ID Generation**: `IDrnSaveChangesInterceptor` assigns collision-free `long` IDs during `SaveChangesAsync` for new entities (`Id = 0`).
- **Property Initialization**: `IDrnMaterializationInterceptor` initializes `EntityIdSource` and identity-related delegates (`IdFactory`, `Parser`) during entity materialization from the database.
- **Mechanism**: Powered by `ISourceKnownIdUtils` and `ISourceKnownEntityIdUtils`.
- **Note**: `EntityId` (Guid) and `EntityIdSource` are ignored by EF mapping as they are computed or runtime traits.

### 2. JSON Models (`IEntityWithModel`)
Entities implementing `IEntityWithModel<TModel>` (e.g. `AggregateRoot<TModel>`) have their `.Model` property automatically mapped to a `jsonb` column:
```csharp
// Automatic configuration applied by DrnContext:
entityTypeBuilder.OwnsOne(ownedType, "Model", builder => builder.ToJson());
```

Usage:
```csharp
[EntityType(3)]
public class SystemSettings : AggregateRoot<SettingsModel> { }

public class SettingsModel
{
    public string Theme { get; set; }
    public int MaxRetries { get; set; }
}
```

### 3. Startup Validation
`[DrnContextServiceRegistration]` attribute validates that all registered entities have a valid `[EntityType]` attribute, preventing startup if IDs are missing or duplicated.

### 4. Identity Naming Conventions
`DrnContextIdentity` snake_case table renaming convention is applied when ASP.NET Core Identity types are mapped. All Identity entity names and columns are converted to `snake_case` for PostgreSQL compatibility.

---

## Connection String Conventions

### Non-Development (Production/Staging)

**Explicit connection strings are required.** The framework calls `appSettings.GetRequiredConnectionString(contextName)`.

```json
{
  "ConnectionStrings": {
    "QAContext": "Host=prod-db.example.com;Port=5432;Database=qa_prod;User ID=qa_user;Password=***;..."
  }
}
```

> [!CAUTION]
> `postgres-password` and all `DrnContext_Dev*` settings are **ignored** in non-Development environments. Missing connection strings will throw `ConfigurationException`.

### Local Debug with LaunchExternalDependencies

When `DrnDevelopmentSettings:LaunchExternalDependencies = true`, the framework uses Testcontainers to automatically start PostgreSQL.

**Setup** (requires `DRN.Framework.Testing` reference in Debug mode):

```xml
<!-- In your .csproj -->
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
    <ProjectReference Include="..\DRN.Framework.Testing\DRN.Framework.Testing.csproj" />
</ItemGroup>
```

**Implementation**:
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

**Key Points**:
- `postgres-password` is **not used** — containers use `PostgresContainerSettings.DefaultPassword` (`"drn"`)
- Connection strings are automatically injected into configuration
- `Reuse = true` keeps the container running across application restarts

### Containerized Development (Docker Compose / Kubernetes)

For development with external database containers, use `postgres-password` to trigger auto-connection string generation.

**Docker Compose Example**:
```yaml
services:
  app:
    build: .
    environment:
      - Environment=Development
      - postgres-password=dev-password  # Triggers auto-connection string
      - DrnContext_DevHost=postgres      # Service name
      - DrnDevelopmentSettings:AutoMigrate=true
    depends_on:
      - postgres
      
  postgres:
    image: postgres:18
    environment:
      POSTGRES_USER: drn
      POSTGRES_PASSWORD: dev-password
      POSTGRES_DB: drn
    ports:
      - "5432:5432"
```

**Connection String Generation** (from `DrnContextDevelopmentConnection`):

When `postgres-password` is set, the framework auto-generates:
```
Host={DrnContext_DevHost};Port={DrnContext_DevPort};Database={DrnContext_DevDatabase};
User ID={DrnContext_DevUsername};password={postgres-password};Multiplexing=true;...
```

### Development Configuration Keys

| Key | Default | Source | Purpose |
|-----|---------|--------|---------|
| `DrnContext_DevHost` | `drn` | `DbContextConventions.DevHostKey` | Database host |
| `DrnContext_DevPort` | `5432` | `DbContextConventions.DevPortKey` | Database port |
| `DrnContext_DevUsername` | `drn` | `DbContextConventions.DevUsernameKey` | Database user |
| `DrnContext_DevDatabase` | `drn` | `DbContextConventions.DefaultDatabaseKey` | Database name |
| `postgres-password` | *(required)* | `DbContextConventions.DevPasswordKey` | Triggers auto-connection string |

### DrnTestContext (Integration Tests)

For integration tests, `ContainerContext` manages Postgres containers automatically.

```csharp
[Theory]
[DataInline]
public async Task Integration_Test(DrnTestContext context)
{
    context.ServiceCollection.AddSampleInfraServices();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();
    
    var dbContext = context.GetRequiredService<QAContext>(); // ... test code

}
```

**Key Points**:
- `DrnContext_Dev*` settings are **NOT used** — containers use `PostgresContainerSettings` defaults
- Connection strings from containers are automatically injected
- `TemporaryApplication` and `DrnTestContextEnabled` are auto-set to prevent collision with local dev

---

## Migrations

### Create Migration

```bash
dotnet ef migrations add MigrationName --context QAContext --project Sample.Infra
```

### Update Database

```bash
dotnet ef database update --context QAContext
```

### Design-Time Support

DrnContext implements `IDesignTimeDbContextFactory<T>` and `IDesignTimeServices`:
- Migrations generate in context-specific folders
- Supports multi-context projects

> [!TIP]
> **Migration Startup Project**: When adding or applying migrations, the project containing the `DrnContext` should be used as the startup project (e.g., `--project Sample.Infra --startup-project Sample.Infra`). This is because the context already implements `IDesignTimeDbContextFactory`.

---

## Prototype Mode

Prototype mode automatically recreates the database when model changes are detected — ideal for rapid prototyping.

**Three conditions must ALL be true**:

1. `NpgsqlDbContextOptionsAttribute.UsePrototypeMode = true` on the context attribute
2. `DrnDevelopmentSettings:Prototype = true`
3. `DrnDevelopmentSettings:LaunchExternalDependencies = true`

If any condition is false, the database is **never** recreated.

**appsettings.Development.json**:
```json
{
  "DrnDevelopmentSettings": {
    "LaunchExternalDependencies": true,
    "AutoMigrate": true,
    "Prototype": true
  }
}
```

> [!WARNING]
> Prototype mode **drops and recreates** the database. Never use in production!

---

## Entity Configuration

Configurations are auto-discovered from the context's namespace:

```csharp
// In Sample.Infra (same namespace as QAContext)
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(100);
    }
}
```

### DB Context Attributes

| Attribute | Purpose |
|-----------|---------|
| `[NpgsqlDbContextOptions]` | Configure Postgres specific options (NpgsqlOptions, DataSource, DbContextOptions), UsePrototypeMode, SeedAsync |
| `[DrnContextDefaults]` | Apply standard DRN conventions (Naming, Discovery) |
| `[DrnContextPerformanceDefaults]` | Apply performance optimizations (see below) |

#### NpgsqlDbContextOptions Configuration Hooks

```csharp
public virtual void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? serviceProvider);
public virtual void ConfigureNpgsqlDataSource<TContext>(NpgsqlDataSourceBuilder builder, IServiceProvider serviceProvider);
public virtual void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder builder, IServiceProvider? serviceProvider);
public virtual Task SeedAsync<TContext>(TContext context, IServiceProvider serviceProvider) where TContext : DbContext;
```

#### DrnContextPerformanceDefaults

Applies optimized PostgreSQL settings by default:

| Setting | Default Value | Purpose |
|---------|---------------|---------|
| `UseNoTracking` | `true` | Disable change tracking globally |
| `IgnoreAutoIncludes` | `true` | Prevent automatic eager loading |
| `Multiplexing` | `true` | Connection multiplexing |
| `MinPoolSize` | `1` | Minimum connection pool |
| `MaxPoolSize` | `10` | Maximum connection pool |
| `ReadBufferSize` | `32768` | I/O buffer size |
| `WriteBufferSize` | `32768` | I/O buffer size |

Custom performance attributes can inherit `NpgsqlPerformanceSettingsAttribute` and override these defaults.

---

## SourceKnownRepository Implementation

`SourceKnownRepository<TContext, TEntity>` implements `ISourceKnownRepository<TEntity>`:

- Uses `IEntityUtils` for ID validation and parsing.
- Implements `GetAsync`, `GetOrDefaultAsync` using `SourceKnownEntityId` validation.
- Provides automatic `ScopedLog` measurements for operations.
- Supports **Behavior Overrides**: Override `EntitiesWithAppliedSettings` to apply global query transformations (e.g., `Include`, nested `ThenInclude`) across all retrieval methods.

### RepositorySettings

Configure repository-wide query behavior:

```csharp
public class RepositorySettings<TEntity>
{
    public bool AsNoTracking { get; init; } = true;
    public bool IgnoreAutoIncludes { get; init; } = true;
    public IReadOnlyList<Expression<Func<TEntity, bool>>>? Filters { get; init; }
}
```

**Override Example** (includes with nested relationships):
```csharp
public class UserRepository : SourceKnownRepository<QAContext, User>
{
    protected override IQueryable<User> EntitiesWithAppliedSettings => 
        base.EntitiesWithAppliedSettings.Include(u => u.Profile).ThenInclude(p => p.Address);
}
```

### Pagination Overloads

```csharp
// Simple
Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null);

// Full control (Jump to, manual total count, etc.)
Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationResultInfo? resultInfo = null, long jumpTo = 1, int pageSize = -1, int maxSize = -1, PageSortDirection direction = PageSortDirection.None, long totalCount = -1, bool updateTotalCount = false);
```

- See [drn-sharedkernel.md](../drn-sharedkernel/SKILL.md) for the interface definition.

---

## Service Registration

### HasDrnContextServiceCollectionModuleAttribute

```csharp
[DrnContextServiceRegistration, DrnContextDefaults, DrnContextPerformanceDefaults]
public abstract class DrnContext<TContext> : DbContext, 
    IDesignTimeDbContextFactory<TContext>, 
    IDesignTimeServices 
    where TContext : DrnContext<TContext>, new()
```

### Post-Startup Validation

After service validation, if `AutoMigrate` is enabled in Development:
```csharp
await context.Database.MigrateAsync();
```

---

## Testing Integration

DrnTestContext's ContainerContext automatically:
1. Starts PostgreSQL testcontainer
2. Scans service collection for DrnContexts
3. Adds connection strings for each context
4. Applies migrations

```csharp
[Theory]
[DataInline]
public async Task Test(DrnTestContext context)
{
    context.ServiceCollection.AddInfraServices();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();
    
    var qaContext = context.GetRequiredService<QAContext>(); // Ready to test
}
```

---

## Configuration Settings Reference

### Migration and Prototype Settings

| Setting | Default | Source | Purpose |
|---------|---------|--------|---------|
| `AutoMigrate` | `false` | `DrnDevelopmentSettings.AutoMigrate` | Enables migration flow |
| `Prototype` | `false` | `DrnDevelopmentSettings.Prototype` | Enables DB recreation on model changes |
| `LaunchExternalDependencies` | `false` | `DrnDevelopmentSettings.LaunchExternalDependencies` | Launches Testcontainers |
| `TemporaryApplication` | `false` | `DrnDevelopmentSettings.TemporaryApplication` | **Auto-set by tests** to prevent collision |

### DrnDevelopmentSettings Class

```csharp
public class DrnDevelopmentSettings
{
    public bool SkipValidation { get; init; }
    public bool TemporaryApplication { get; init; }
    public bool LaunchExternalDependencies { get; init; }
    public bool AutoMigrate { get; init; }
    public bool Prototype { get; init; }
}
```

### Testcontainers Defaults

When using `LaunchExternalDependencies` or `ContainerContext`, these values from `PostgresContainerSettings` are used:

| Property | Default | Notes |
|----------|---------|-------|
| `DefaultPassword` | `"drn"` | Container password |
| `DefaultImage` | `"postgres"` | Docker image |
| `DefaultVersion` | `"18.1-alpine3.23"` | Image tag |
| `Database` | `"drn"` | From `DbContextConventions.DefaultDatabase` |
| `Username` | `"drn"` | From `DbContextConventions.DefaultUsername` |

---

## Global Usings

```csharp
global using DRN.Framework.EntityFramework.Context;
global using Microsoft.EntityFrameworkCore;
global using DRN.Framework.Utils.DependencyInjection;
```

---

## Related Skills

- [drn-domain-design.md](../drn-domain-design/SKILL.md) - Domain & Repository patterns
- [drn-sharedkernel.md](../drn-sharedkernel/SKILL.md) - Entity base classes
- [drn-utils.md](../drn-utils/SKILL.md) - DI and configuration
- [drn-testing.md](../drn-testing/SKILL.md) - ContainerContext
- [test-integration.md](../test-integration/SKILL.md) - Database testing
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Domain modeling

---