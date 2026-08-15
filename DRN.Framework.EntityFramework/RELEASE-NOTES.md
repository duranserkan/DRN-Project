Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.9.9

Version alignment release; no package-specific behavior changes.

## Version 0.9.8

Dependencies upgraded to dotnet 10.0.11

## Version 0.9.7

### Breaking Changes

*   **Repository Cancellation Scope Key Override Removed**: Removed protected virtual `RepositoryCancellationScopeKey` from `SourceKnownRepository`. Configure `Settings.ScopeKey` instead. A `null` key now uses `Utils.Cancellation.Root`, so `CancelChanges()` and `CancelWhen(token)` affect every operation linked to the root cancellation scope; set an explicit key to isolate a repository group.

### Changed

*   **Package Documentation**: Corrected Entity Framework setup, identity boundaries, repository filtering, prototype safety, migration and seeding, environment configuration, and container guidance.

### Bug Fixes

*   **DbContext Configuration Discovery**: `DbContextExtensions.ModelCreatingDefaults` now matches entity configurations using exact-or-ordinal-child namespace matching (`Equals` or `StartsWith` with dot delimiter) and safely handles `null` namespaces, preventing accidental configuration discovery from prefix-sibling namespaces (e.g. `Acme.Database` for `Acme.Data`) or `NullReferenceException`.
*   **Repository Entity-ID Cross-Entity Substitution**: Fixed `GetOrDefaultAsync` in `SourceKnownRepository` so querying with `validate: false` for a `SourceKnownEntityId` or `Guid` of a different entity type returns `null` instead of querying by internal numeric ID.
*   **Auto-Migration Seeding**: Fixed `DrnContextServiceRegistrationAttribute.PostStartupValidationAsync` so `SeedData` is invoked whenever pending migrations are applied, regardless of whether previous migrations were already applied to the database. Documented that duplicate seed prevention/idempotency is the responsibility of `SeedAsync` implementations, not `DRN.Framework.EntityFramework`.


## Version 0.9.6

### Breaking Changes

*   **NexusKey BLAKE3 Derivation**: `NexusKey` now derives both `MacKey` and `EncryptionKey` from decoded 32-byte key material through BLAKE3 derive-key mode with distinct DRN Framework context strings. This replaces the previous custom hash-chain derivation and changes generated secure IDs; existing IDs may require migration, regeneration, or an explicit compatibility strategy.
*   **Repository Cancellation API**: `SourceKnownRepository.CancellationToken` is now read-only and `MergeCancellationTokens` was replaced by the explicit `CancelWhen(token)` lifetime-linking method. `CancelChanges` no longer cancels `ICancellationUtils.Root`; callers that relied on cancel-all behavior must migrate to `cancellation.Root.Cancel()` or `cancellation.Root.Merge(token)`.

### New Features

*   **Repository Cancellation Scopes**: The default repository implementation groups cancellation by concrete repository type within the current DI scope. Instances of the same type cancel together; override the non-nullable protected virtual `RepositoryCancellationScopeKey` to select another shared group.

### Bug Fixes

*   **Repository Query Correctness**: `SourceKnownRepository.CountAsync` now preserves its 64-bit contract with `LongCountAsync`, and generated queries retain both repository and caller SQL tags.

## Version 0.9.5

### Breaking Changes

*   **Npgsql Multiplexing Removal**: Deprecated Npgsql multiplexing configuration has been removed from performance attributes and generated development connection strings. Consumers passing the `multiplexing` constructor argument to `NpgsqlPerformanceSettingsAttribute` or `DrnContextPerformanceDefaultsAttribute` must remove it.

### Changed

*   **PostgreSQL Defaults**: DRN's Npgsql context defaults now target PostgreSQL 18.4 instead of 18.2 when configuring provider compatibility.

### Bug Fixes

*   **Repository Entity-ID Validation**: `SourceKnownRepository` now validates collection `SourceKnownEntityId` inputs against the repository entity type before query/delete filters, preventing cross-entity IDs from matching by internal source ID.

## Version 0.9.4

Dependencies upgraded to dotnet 10.0.8

## Version 0.9.3

Dependencies upgraded to dotnet 10.0.7

## Version 0.9.2

Dependencies upgraded to dotnet 10.0.6

## Version 0.9.1

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and is proud to inherit his spiritual legacy: 'I am not leaving behind any definitive text, any dogma, any frozen, rigid rule as my spiritual legacy. My spiritual wealth is science and reason. Those who wish to embrace me after my death will become my spiritual heirs if they accept the guidance of reason and science on this fundamental axis.'

## Version 0.9.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and stands behind his remarkable words: 'Peace at home, peace in the world.'

## Version 0.8.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals, rooted in his timeless words that 'science is the truest guide in life.' In that spirit, and to honor the 14 March Scientists Day, this release is dedicated to the researchers working for the benefit of humanity, and to the rejection of my first academic paper :) ([JOSS #10176](https://github.com/openjournals/joss-reviews/issues/10176)).

## Version 0.7.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and honors 8 March, International Women's Day, a cause inseparable from his vision of equality. This release is dedicated to freedom of speech, democracy, women's rights, and Prof. Dr. Ümit Özdağ, a defender of Mustafa Kemal Atatürk’s enlightenment ideals.

> [!WARNING]
> Since v0.6.0 (released 10 November 2024), substantial changes have occurred. This release notes file has been reset to reflect the current state of the project as of 08 March 2026. Previous history has been archived to maintain a clean source of truth based on the current codebase.

### New Features

*   **DrnContext & Convention Pattern**
    *   **Zero-Config Registration**: `DrnContext<T>` automatically handles service registration, connection string resolution (`ConnectionStrings:ContextName`), and configuration discovery.
    *   **Design-Time Support**: Implements `IDesignTimeDbContextFactory<T>` and `IDesignTimeServices` for seamless migration generation in context-specific folders.
    *   **Startup Validation**: `[DrnContextServiceRegistration]` validates entity types and scopes at startup.
    *   **Custom Migration Scaffolding**: `DrnMigrationsScaffolder` automatically organizes migrations into context-specific `Migrations/` folders, keeping the project structure clean.
    *   **Identity Table Conventions**: `DrnContextIdentity` maps standard Identity tables to clean snake_case names (e.g., `users`, `user_logins`, `role_claims`, `user_tokens`).
*   **Augmented Entity Behavior**
    *   **Auto-Tracking & Lifecycle**:
        - Automatic `CreatedAt`/`ModifiedAt` management.
        - Injection of `ISourceKnownEntityIdOperations` (`EntityIdOps`) during materialization/save for type-safe ID operations.
    *   **Domain Events**: Entity lifecycle events are collected on entities; dispatching/publishing is not performed by this package.
*   **SourceKnownRepository**
    *   **Complete Implementation**: `SourceKnownRepository<TContext, TEntity>` providing standard CRUD, identity validation, and logging.
    *   **Repository Settings**: `AsNoTracking`, `IgnoreAutoIncludes`, and custom `Filters` support via `RepositorySettings`.
    *   **Advanced Pagination**: Cursor-based pagination (`PaginateAsync`) and infinite scrolling (`PaginateAllAsync`) with `EntityCreatedFilter`.
    *   **Secure ↔ Plain Conversion**: `ToSecure` / `ToPlain` methods for converting between encrypted and plaintext entity IDs at the repository level.
*   **Database Configuration**
    *   **Npgsql Optimization**: `[DrnContextPerformanceDefaults]` configures pooling, batching, query splitting, command timeouts, and other Npgsql performance defaults.
    *   **Extensive Defaults**: `[DrnContextDefaults]` configures snake_case naming, JSON options, and places migration history in `__entity_migrations` schema.
    *   **Customizable Options**: `NpgsqlDbContextOptionsAttribute` allows overriding Npgsql (`ConfigureNpgsqlOptions`) and generic DbContext settings.
*   **Development Experience**
    *   **Auto-Connection Strings**: Automatically generates connection strings for local Docker containers (e.g., `Host=postgresql;Port=5432...`) if missing.
    *   **Configurable Keys**: Supports overrides via `DrnContext_DevHost`, `DrnContext_DevUsername`, `DrnContext_DevDatabase`.
    *   **Prototype Mode**: Development-only database recreation on model changes when `UsePrototypeMode=true`, `DrnDevelopmentSettings:Prototype=true`, and `AutoMigrateDevelopment=true`.
    *   **Auto-Migration**: `DrnDevelopmentSettings:AutoMigrateDevelopment` (default `true`) and `AutoMigrateStaging` (default `false`) apply pending migrations per environment at startup.

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/.agent/rules/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
