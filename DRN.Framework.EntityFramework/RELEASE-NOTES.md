Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.7.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals.

> [!WARNING]
> Since v0.6.0 (released 10 November 2024), substantial changes have occurred. This release notes file has been reset to reflect the current state of the project as of 29 January 2026. Previous history has been archived to maintain a clean source of truth based on the current codebase.

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
        - Initialization of `IdFactory` and `Parser` during materialization/save for type-safe ID operations.
    *   **Domain Events**: Events are collected and automatically published after `SaveChangesAsync`.
*   **SourceKnownRepository**
    *   **Complete Implementation**: `SourceKnownRepository<TContext, TEntity>` providing standard CRUD, identity validation, and logging.
    *   **Repository Settings**: `AsNoTracking`, `IgnoreAutoIncludes`, and custom `Filters` support via `RepositorySettings`.
    *   **Advanced Pagination**: Cursor-based pagination (`PaginateAsync`) and infinite scrolling (`PaginateAllAsync`) with `EntityCreatedFilter`.
*   **Database Configuration**
    *   **Npgsql Optimization**: `[DrnContextPerformanceDefaults]` enables connection multiplexing, pooling, batching, and query splitting.
    *   **Extensive Defaults**: `[DrnContextDefaults]` configures snake_case naming, JSON options, and places migration history in `__entity_migrations` schema.
    *   **Customizable Options**: `NpgsqlDbContextOptionsAttribute` allows overriding Npgsql (`ConfigureNpgsqlOptions`) and generic DbContext settings.
*   **Development Experience**
    *   **Auto-Connection Strings**: Automatically generates connection strings for local Docker containers (e.g., `Host=postgresql;Port=5432...`) if missing.
    *   **Configurable Keys**: Supports overrides via `DrnContext_DevHost`, `DrnContext_DevUsername`, `DrnContext_DevDatabase`.
    *   **Prototype Mode**: Auto-recreates database on model changes when `UsePrototypeMode=true` and `DrnDevelopmentSettings:Prototype=true`.
    *   **Auto-Migration**: `DrnContext_AutoMigrateDevEnvironment` applies pending migrations at startup.

---

**Semper Progressivus: Always Progressive**