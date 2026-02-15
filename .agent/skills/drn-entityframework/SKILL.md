---
name: drn-entityframework
description: DRN.Framework.EntityFramework - DrnContext base class, automatic migration application, entity tracking with domain events, NpgsqlDbContextOptions for database configuration, and repository implementations. Essential for database operations, migrations, and data persistence. Keywords: drncontext, ef-core, migrations, database, postgresql, npgsql, repository-implementation, entity-tracking, domain-events, dbcontext-configuration, prototype-mode, testcontainers
last-updated: 2026-02-15
difficulty: advanced
---

# DRN.Framework.EntityFramework

> Convention-based EF Core integration with automatic configuration and migrations.

## When to Apply
- Creating new DbContexts
- Setting up database migrations
- Configuring development database connections
- Understanding entity tracking and domain events
- Working with Testcontainers for local development

---

## DrnContext Pattern

```csharp
public class QAContext : DrnContext<QAContext>
{
    public QAContext(DbContextOptions<QAContext> options) : base(options) { }
    public QAContext() : base(null) { }  // Required for migrations

    public DbSet<User> Users { get; set; }
    public DbSet<Question> Questions { get; set; }
}
```

| Feature | Description |
|---------|-------------|
| **Auto-registration** | Via `AddServicesWithAttributes()` |
| **Convention naming** | Connection string: `ConnectionStrings:QAContext` |
| **Config discovery** | Auto-applies `IEntityTypeConfiguration` from context namespace |
| **Entity tracking** | Auto-marks entities as Created/Modified/Deleted with timestamps |
| **Design-time** | Implements `IDesignTimeDbContextFactory` |

> Configurations are auto-discovered: place `IEntityTypeConfiguration<T>` in the same namespace as the `DrnContext` subclass.

### Augmented Entity Behavior

DrnContext augments entities during `OnModelCreating` and runtime:

| Feature | Mechanism |
|---------|-----------|
| **ID Generation** | `IDrnSaveChangesInterceptor` assigns collision-free `long` IDs for new entities |
| **Property Init** | `IDrnMaterializationInterceptor` initializes `EntityIdSource` and identity delegates |
| **JSON Models** | `IEntityWithModel<T>` auto-maps `.Model` to `jsonb` column |
| **Identity Naming** | ASP.NET Core Identity tables/columns → `snake_case` for PostgreSQL |
| **Startup Validation** | Validates all entities have valid, unique `[EntityType]` attributes |

---

## Connection String Conventions

### Production/Staging
Explicit connection strings required via `appSettings.GetRequiredConnectionString(contextName)`:
```json
{ "ConnectionStrings": { "QAContext": "Host=prod-db;Port=5432;Database=qa;..." } }
```

### Local Dev with Testcontainers (`LaunchExternalDependencies`)

When `DrnDevelopmentSettings:LaunchExternalDependencies = true`, the framework auto-starts PostgreSQL via Testcontainers.

**Setup** (Debug-only reference to `DRN.Framework.Testing`):
```xml
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
    <ProjectReference Include="..\DRN.Framework.Testing\DRN.Framework.Testing.csproj" />
</ItemGroup>
```

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
                Reuse = true,   // Keep container across restarts
                HostPort = 6432 // Avoid port conflicts
            }
        };
        await builder.LaunchExternalDependenciesAsync(scopedLog, appSettings, launchOptions);
    }
}
#endif
```

### Containerized Development (Docker Compose)

Set `postgres-password` env var to trigger auto-connection string generation:
```
Environment=Development, postgres-password=dev-password, DrnContext_DevHost=postgres
```

| Key | Default | Purpose |
|-----|---------|---------|
| `DrnContext_DevHost` | `drn` | Database host |
| `DrnContext_DevPort` | `5432` | Database port |
| `DrnContext_DevUsername` | `drn` | Database user |
| `DrnContext_DevDatabase` | `drn` | Database name |
| `postgres-password` | *(required)* | Triggers auto-connection string |

> [!CAUTION]
> `postgres-password` and `DrnContext_Dev*` keys are **ignored** in non-Development environments.

---

## Migrations

```bash
dotnet ef migrations add MigrationName --context QAContext --project Sample.Infra
dotnet ef database update --context QAContext
```

> [!TIP]
> Use the project containing `DrnContext` as startup project — it implements `IDesignTimeDbContextFactory`.

---

## Prototype Mode

Auto-recreates database on model changes. **All three conditions must be true**:
1. `NpgsqlDbContextOptionsAttribute.UsePrototypeMode = true`
2. `DrnDevelopmentSettings:Prototype = true`
3. `DrnDevelopmentSettings:LaunchExternalDependencies = true`

> [!WARNING]
> Prototype mode **drops and recreates** the database. Never use in production.

---

## Entity Configuration

Configurations are auto-discovered from the context's assembly namespace:

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

---

## DB Context Attributes

| Attribute | Purpose |
|-----------|---------|
| `[NpgsqlDbContextOptions]` | Postgres options, UsePrototypeMode, SeedAsync |
| `[DrnContextDefaults]` | Standard DRN conventions (naming, discovery) |
| `[DrnContextPerformanceDefaults]` | Performance optimizations (see below) |

#### NpgsqlDbContextOptions Configuration Hooks

```csharp
public virtual void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? serviceProvider);
public virtual void ConfigureNpgsqlDataSource<TContext>(NpgsqlDataSourceBuilder builder, IServiceProvider serviceProvider);
public virtual void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder builder, IServiceProvider? serviceProvider);
public virtual Task SeedAsync<TContext>(TContext context, IServiceProvider serviceProvider) where TContext : DbContext;
```

### Performance Defaults

| Setting | Default | Purpose |
|---------|---------|---------|
| `UseNoTracking` | `true` | Disable change tracking |
| `IgnoreAutoIncludes` | `true` | Prevent auto eager loading |
| `Multiplexing` | `true` | Connection multiplexing |
| `MinPoolSize`/`MaxPoolSize` | `1`/`10` | Connection pool |
| `Read/WriteBufferSize` | `32768` | I/O buffers |

Custom performance attributes can inherit `NpgsqlPerformanceSettingsAttribute` to override defaults.

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

> Override `EntitiesWithAppliedSettings` in custom repositories for global includes/filters. See [drn-domain-design](../drn-domain-design/SKILL.md) for examples.

---

## DrnDevelopmentSettings

```csharp
public class DrnDevelopmentSettings
{
    public bool SkipValidation { get; init; }
    public bool TemporaryApplication { get; init; }       // Auto-set by tests
    public bool LaunchExternalDependencies { get; init; }
    public bool AutoMigrate { get; init; }
    public bool Prototype { get; init; }
}
```

### Testcontainer Defaults (`PostgresContainerSettings`)

| Property | Default | Notes |
|----------|---------|-------|
| `DefaultPassword` | `"drn"` | Container password |
| `DefaultImage` | `"postgres"` | Docker image |
| `DefaultVersion` | `"18.1-alpine3.23"` | Image tag |
| `Database` | `"drn"` | From `DbContextConventions.DefaultDatabase` |
| `Username` | `"drn"` | From `DbContextConventions.DefaultUsername` |

---

## Testing Integration

> See [drn-testing](../drn-testing/SKILL.md) for full `ContainerContext` details.

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

## Related Skills

- [drn-domain-design.md](../drn-domain-design/SKILL.md) - Domain & Repository patterns
- [drn-sharedkernel.md](../drn-sharedkernel/SKILL.md) - Entity base classes
- [drn-utils.md](../drn-utils/SKILL.md) - DI and configuration
- [drn-testing.md](../drn-testing/SKILL.md) - ContainerContext
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Domain modeling

---

## Global Usings

```csharp
global using DRN.Framework.EntityFramework.Context;
global using Microsoft.EntityFrameworkCore;
global using DRN.Framework.Utils.DependencyInjection;
```
