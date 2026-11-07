---
name: drn-entityframework
description: "Configure DRN PostgreSQL contexts, identity tracking, migrations, prototype safety, repositories and cancellation."
last-updated: 2026-11-07
difficulty: advanced
---

# DRN.Framework.EntityFramework

Read the installed `DRN.Framework.EntityFramework` package README for connection examples, attribute hooks and provider defaults. [Locate the package](../overview-drn-framework/SKILL.md#documentation-and-source-references)

## DrnContext Pattern

Derive the context from `DrnContext<TContext>`. Provide the options constructor and parameterless constructor for migrations. Register via attribute scanning. Use the context type name for its connection-string key. Put entity configurations in the same assembly, in the context namespace or a child namespace.

```csharp
public class OrdersContext : DrnContext<OrdersContext>
{
    public OrdersContext(DbContextOptions<OrdersContext> options) : base(options) { }
    public OrdersContext() : base(null) { }
    public DbSet<Order> Orders => Set<Order>();
}
```

Scan the application's context assembly with `builder.Services.AddServicesWithAttributes(typeof(OrdersContext).Assembly)`. The named connection is `ConnectionStrings:OrdersContext`. DRN Hosting runs startup validation. Standalone hosts must integrate the framework's startup validation lifecycle explicitly.

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

## Connection String Conventions

| Environment | Connection source |
|---|---|
| Production or Staging | Explicit `ConnectionStrings:{ContextName}`. Development keys are ignored. |
| Local Development with Testcontainers | Enable `LaunchExternalDependencies` and invoke the launch hook. `PostgresContainerSettings` supplies container settings. |
| Containerized Development | `DrnContextDevelopmentConnection` builds the connection from `postgres-password` and `DrnContext_Dev*` settings. |
| Integration tests | Bind `ContainerContext.Postgres` to the test application. Start RabbitMQ explicitly when needed. |

In Development, an explicit named connection takes precedence. Without a named connection or the development password, resolution fails.

## Migrations

Compose attribute `SeedAsync` after any custom EF seeding callback. Keep seeds idempotent and use the same scoped context. Eligible automatic startups call `MigrateAsync` even with zero pending migrations. Migration operations run seeds under the EF migration lock and a later startup may retry failed seeding. Explicit DI migration/creation calls seed too. Design-time contexts without DI omit attribute seeding. Prototype creation has no migration-lock guarantee.

Reapplying options replaces DRN wrappers with the latest provider and preserves custom callbacks. Without a provider only custom callbacks remain. Use the context project for design-time startup. Resolve migration commands from the profile and follow the exact-command approval gate.

## Prototype Mode

Prototype recreation drops the database. Allow it only in Development with all of the following:

- `AutoMigrateDevelopment`, `DrnDevelopmentSettings:Prototype`, and the context attribute's `UsePrototypeMode` are enabled.
- Pending model changes exist.
- No migrations have been applied, or the context explicitly enables `UsePrototypeModeWhenMigrationExists`.

Staging can opt into migrations through `AutoMigrateStaging`. Production never auto-migrates. Neither environment permits prototype recreation.

## Entity Configuration

Prefer attributes for ordinary mappings. Use Fluent API for complex relationships. Configure the relationship before navigation options such as `AutoInclude()`. [EF Core ordering example](https://github.com/dotnet/efcore/issues/31380)

Use `NpgsqlDbContextOptionsAttribute` for provider, data source, context and seed hooks. `DrnContextDefaults` applies DRN conventions. Consult the EntityFramework README before overriding pooling, buffers, timeouts and statement preparation through a performance settings attribute.

### RepositorySettings

Build custom queries from `EntitiesWithAppliedSettings()` to preserve filters, tracking and auto-include policy. Use named filters for repository-wide conditions. Public CRUD, query and pagination methods are virtual. Protected pagination overloads are not.

GUID methods validate the declared entity type and partition, including secondary partitions. The configured default resolves in the parser. Keep Plain/Auto overrides under application control and accept parsed records only from trusted callers. Undefined GUID formats reject nulls and empty batches. Enumerable helpers validate when enumerated. Record and entity inputs have no format argument. Keep mixed-format batch deletes atomic on rejection. [SharedKernel validation](../drn-sharedkernel/SKILL.md#validation-approaches)

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

For one operation, dispose the local linked source after the work completes. For group ownership, see [scoped cancellation](../drn-utils/SKILL.md#scoped-cancellation).
