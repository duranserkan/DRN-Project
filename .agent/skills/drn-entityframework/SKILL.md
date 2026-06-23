---
name: drn-entityframework
description: "DRN.Framework.EntityFramework - DrnContext base class, automatic migration application, entity lifecycle tracking, NpgsqlDbContextOptions for database configuration, and repository implementations. Essential for database operations, migrations, and data persistence. Keywords: drncontext, ef-core, migrations, database, postgresql, npgsql, repository-implementation, entity-tracking, dbcontext-configuration, prototype-mode, testcontainers"
last-updated: 2026-06-12
difficulty: advanced
tokens: ~2.5K
---

# DRN.Framework.EntityFramework

> Convention-based EF Core integration with automatic configuration and migrations.

## When to Apply
- Creating new DbContexts
- Setting up database migrations
- Configuring development database connections
- Understanding Source-Known entity lifecycle tracking
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
| **Config discovery** | Auto-applies `IEntityTypeConfiguration` from the context assembly when the configuration namespace contains the context namespace |
| **Entity tracking** | Auto-marks entities as Created/Modified/Deleted with timestamps |
| **Design-time** | Implements `IDesignTimeDbContextFactory` |

> Configurations are auto-discovered from the context assembly when the configuration namespace contains the `DrnContext` namespace. Child namespaces such as `Sample.Infra.QA.Configurations` are valid for a `Sample.Infra.QA` context.

### Augmented Entity Behavior

DrnContext augments entities during `OnModelCreating` and runtime:

| Feature | Mechanism |
|---------|-----------|
| **ID Generation** | `IDrnSaveChangesInterceptor` assigns collision-free `long` IDs for new entities |
| **Property Init** | `IDrnMaterializationInterceptor` initializes `EntityIdSource` and injects `ISourceKnownEntityIdOperations` (`EntityIdOps`) |
| **Secure ↔ Plain** | `ToSecure` / `ToPlain` on entity and repository for idempotent ID form conversion |
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

Auto-recreates the local development database on pending model changes. **All conditions must be true**:
1. `NpgsqlDbContextOptionsAttribute.UsePrototypeMode = true`
2. `DrnDevelopmentSettings:Prototype = true`
3. Application environment is Development
4. `DrnDevelopmentSettings:AutoMigrateDevelopment = true`
5. Pending model changes exist
6. No migrations have been applied, or applied migrations exist and `UsePrototypeModeWhenMigrationExists = true`

> [!WARNING]
> Prototype mode **drops and recreates** the database. It is Development-only; `AutoMigrateStaging` applies migrations only and must not enable prototype recreation.
>
> Canonical invariants: [Maintenance Reference: Migration And Prototype Invariants](../overview-drn-framework/SKILL.md#maintenance-reference-migration-and-prototype-invariants).

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

> [!CAUTION]
> **Navigation Configuration Ordering**: Navigation-level configurations (e.g., `AutoInclude()`) must be placed **after** relationship definitions in Fluent API. Placing them before silently fails. See [dotnet/efcore#31380](https://github.com/dotnet/efcore/issues/31380).
>
> ```csharp
> // ✗ WRONG — AutoInclude silently ignored
> builder.Navigation(x => x.Books).AutoInclude();
> builder.HasMany(x => x.Books).WithMany();
>
> // ✓ CORRECT — define relationship first, then configure navigation
> builder.HasMany(x => x.Books).WithMany();
> builder.Navigation(x => x.Books).AutoInclude();
> ```

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
public virtual Task SeedAsync(IServiceProvider serviceProvider, IAppSettings appSettings);
```

### Performance Defaults

| Setting | Default | Purpose |
|---------|---------|---------|
| `MaxAutoPrepare` | `200` | Prepared statement cache size |
| `AutoPrepareMinUsages` | `5` | Minimum usages before auto-prepare |
| `MinPoolSize`/`MaxPoolSize` | `1`/`15` | Connection pool bounds |
| `Read/WriteBufferSize` | `8192` | I/O buffers |
| `CommandTimeout` | `30` | Command timeout in seconds |

Custom performance attributes can inherit `NpgsqlPerformanceSettingsAttribute` to override defaults.

### RepositorySettings

Configure repository-wide query behavior:

```csharp
public class RepositorySettings<TEntity>
{
    public bool AsNoTracking { get; set; }
    public bool IgnoreAutoIncludes { get; set; }
    public IReadOnlyDictionary<string, Expression<Func<TEntity, bool>>> Filters { get; }
    public void AddFilter(string name, Expression<Func<TEntity, bool>> filter);
    public bool RemoveFilter(string name);
    public void ClearFilters();
}
```

> Override `EntitiesWithAppliedSettings` in custom repositories for global includes/filters. See [drn-domain-design](../drn-domain-design/SKILL.md) for examples.

---

## Shared Defaults Reference

Canonical `DrnDevelopmentSettings`, connection modes, prototype invariants, and container defaults live in the [DRN Framework Maintenance Reference](../overview-drn-framework/SKILL.md#drn-framework-maintenance-reference).

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
