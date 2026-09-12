---
name: drn-entityframework
description: "DRN.Framework.EntityFramework - DrnContext, migrations, entity lifecycle tracking, Npgsql configuration, repositories, and repository cancellation groups. Keywords: drncontext, ef-core, migrations, database, postgresql, npgsql, repository-implementation, repository-cancellation, cancellation-scope, entity-tracking, dbcontext-configuration, prototype-mode, testcontainers"
last-updated: 2026-09-12
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
| **Config discovery** | Auto-applies `IEntityTypeConfiguration` from the context assembly when the configuration namespace matches the context namespace or one of its child namespaces |
| **Entity tracking** | Auto-marks entities as Created/Modified/Deleted with timestamps |
| **Design-time** | Implements `IDesignTimeDbContextFactory` |

> Configurations are auto-discovered from the context assembly when the configuration namespace matches the context namespace or one of its child namespaces. Child namespaces such as `Sample.Infra.QA.Configurations` are valid for a `Sample.Infra.QA` context.

### Augmented Entity Behavior

DrnContext augments entities during `OnModelCreating` and runtime:

| Feature | Mechanism |
|---------|-----------|
| **Internal ID Generation** | `SourceKnownIdValueGenerator` assigns collision-free `long` IDs when EF begins tracking new entities; `IDrnSaveChangesInterceptor` provides a save-time fallback when `Id` remains zero |
| **Save-Time Initialization** | `IDrnSaveChangesInterceptor` initializes `EntityIdSource` and `EntityIdOps`, sets `ModifiedAt`, and applies created lifecycle state before persistence |
| **Materialization Init** | `IDrnMaterializationInterceptor` initializes `EntityIdSource` and injects `ISourceKnownEntityIdOperations` (`EntityIdOps`) for loaded entities |
| **Secure ↔ Plain** | `ToSecure` / `ToPlain` on entity and repository for idempotent ID form conversion |
| **JSON Models** | `IEntityWithModel<T>` auto-maps `.Model` to `jsonb` column |
| **Identity Naming** | ASP.NET Core Identity tables/columns → `snake_case` for PostgreSQL |
| **Mapped Inheritance** | Configures SourceKnownEntity keys and shared properties on EF hierarchy roots for TPH/TPT/TPC; derived entities inherit the key and ID generator while retaining their own concrete entity-type metadata |
| **Startup Validation** | Validates concrete, non-private SourceKnownEntity entities have valid, unique `(EntityType, AppId)` pairs while permitting the same entity byte in different application partitions; abstract bases and entities that are private or nested at any depth inside a private type are ignored in model and assembly discovery, matching analyzer eligibility |

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

DI-configured contexts compose attribute `SeedAsync` with EF `UseSeeding`/`UseAsyncSeeding` callbacks, after any custom callback. Eligible automatic startups call `MigrateAsync` even with no pending migrations so seeds run under the migration lock and failed seeds can be retried. Explicit DI migration/creation operations also seed; synchronous operations wait for the asynchronous hook. Design-time configuration without DI does not add attribute seeding. Seeds must be idempotent and use the same scoped context. Prototype creation callbacks do not provide migration-lock concurrency guarantees. Existing environment and pending-model guards still apply.

Reapplying context options preserves custom callbacks and replaces DRN seed wrappers using the latest supplied provider; it does not duplicate attribute seeding. Reconfiguration without a provider restores only custom callbacks.

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
public virtual Task SeedAsync(IServiceProvider serviceProvider, IAppSettings appSettings); // See https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding
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

Repository GUID validation checks the declared `(EntityType, AppId)` of `TEntity` or `TOtherEntity`, including secondary partitions. Nullable inputs preserve null; `validate: false` skips validation.

Repository GUID ID helpers and GUID-input GetAsync/GetOrDefaultAsync methods accept non-nullable `SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault`. The parser resolves ConfiguredDefault through immutable DefaultFormat; explicit Auto permits mixed-format GUID input. GUID DeleteAsync adds an `(ids, format)` collection overload and retains its params overload. Parsed-record reads/deletes and the protected static record-ID Filter have no format parameter: they check stored validity and entity type/application partition, accepting either parsed representation. Use trusted internal records; untrusted GUIDs require parsing. Entity-input operations have no format parameter. `validate: false` skips record validation. Undefined GUID formats reject even null inputs/empty batches; enumerable helpers defer parsing until enumeration. Update custom GUID signatures/overrides and rebuild consumers.

Configure repository-wide query behavior:

```csharp
public class RepositorySettings<TEntity>
{
    public bool AsNoTracking { get; set; }
    public bool IgnoreAutoIncludes { get; set; }
    public CancellationScopeKey? ScopeKey { get; set; }
    public IReadOnlyDictionary<string, Expression<Func<TEntity, bool>>> Filters { get; }
    public void AddFilter(string name, Expression<Func<TEntity, bool>> filter);
    public bool RemoveFilter(string name);
    public void ClearFilters();
}
```

> Override `EntitiesWithAppliedSettings` in custom repositories for global includes/filters. See [drn-domain-design](../drn-domain-design/SKILL.md) for examples.

Public repository CRUD, query, and pagination methods are virtual; protected pagination overloads are not.

### Repository Cancellation

Repository cancellation scope is configured via `Settings.ScopeKey`:

- When `Settings.ScopeKey` is `null` (default), repository cancellation uses `Utils.Cancellation.Root`. `CancelChanges()` cancels `Root`, affecting all scope-wide operations.
- When `Settings.ScopeKey` is set to a `CancellationScopeKey`, operations use that child scope:
  - `CancellationToken` returns the child scope token.
  - `CancelWhen(token)` links a lifetime token to the repository group.
  - `CancelChanges()` cancels only repositories sharing that scope key.

```csharp
repository.Settings.ScopeKey = CancellationScopeKey.For<UserRepository>("shared-writes");

using var operationSource =
    CancellationTokenSource.CreateLinkedTokenSource(
        repository.CancellationToken,
        operationToken);
```

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
