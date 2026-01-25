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

DRN.Framework.EntityFramework provides DrnContext with conventions to develop rapid and effective domain models.

## DRNContext

`DrnContext` is the foundational `DbContext` implementation that integrates with the DRN Framework ecosystem.

*   **Zero-Config Registration**: Uses `[DrnContextServiceRegistration]` for automatic DI registration, including `IDesignTimeDbContextFactory` support for migrations.
*   **Convention-Based Configuration**:
    *   Context name defines the connection string key.
    *   Automatically applies `IEntityTypeConfiguration` from the assembly.
*   **Audit Support**: Automatically manages `IDomainEvent` dispatching and `Tracking` properties (`CreatedAt`, `ModifiedAt`) for `SourceKnownEntity`.
*   **Integration Testing**: Native support for `DRN.Framework.Testing`'s `ContainerContext` for isolated Postgres container tests.

```csharp
[DrnContextServiceRegistration, DrnContextDefaults, DrnContextPerformanceDefaults]
public abstract class DrnContext<TContext> : DbContext, IDrnContext<TContext> where TContext : DrnContext<TContext>, new()
{
    // ...
}
```

## SourceKnownRepository

`SourceKnownRepository<TContext, TEntity>` is the EF Core implementation of `SharedKernel.ISourceKnownRepository`. It provides a production-ready data access layer with built-in performance and consistency checks.

*   **RepositorySettings**:
    *   `AsNoTracking`: Globally enable/disable tracking for queries.
    *   `IgnoreAutoIncludes`: prevent auto-loading of navigation properties for performance.
    *   `Filters`: Apply global LINQ filters (e.g., Soft Delete, Tenancy) automatically to all queries.
*   **Pagination**: Efficient cursor-based pagination using `PaginateAsync`.
*   **Validation**: Validates `SourceKnownEntityId` entity types before query execution.

```csharp
public class UserRepository(MyDbContext context, IEntityUtils utils) 
    : SourceKnownRepository<MyDbContext, User>(context, utils), IUserRepository
{
    // Custom query methods...
}
```

## Attributes & Configuration

### DrnContextServiceRegistrationAttribute

Decorate your `DbContext` with this to enable automatic registration and lifecycle management.

*   **Startup Validation**:
    *   **Scope Check**: Validates that 50+ service scopes can be created rapidly (catches singleton/scoped mismatches early).
    *   **Entity Type Check**: Scans all `SourceKnownEntity` types in the model to ensure they have unique `[EntityType]` attributes.
*   **Auto-Migration & Seeding**:
    *   Detects pending migrations and applies them if configured (`DrnContext_AutoMigrateDevEnvironment`).
    *   Runs `SeedAsync` implementations from registered `NpgsqlDbContextOptionsAttribute`s after migration.

### NpgsqlDbContextOptionsAttribute

Provides Npgsql-specific configuration and enables **Prototype Mode**.

*   **Prototype Mode** `[NpgsqlDbContextOptions(UsePrototypeMode = true)]`:
    *   Designed for rapid development.
    *   **How it works**: If the framework detects pending model changes (e.g., you added a property to an entity) but no corresponding migration exists yet, it will **automatically drop and recreate the local development database**.
    *   **Benefit**: Eliminates the need to create "junk" migrations during the initial prototyping phase.
*   **Configuration Hooks**: Override `ConfigureNpgsqlOptions` or `ConfigureDbContextOptions` to customize the driver.

```csharp
[DrnContextServiceRegistration]
[DrnContextDefaults]
[MyProjectPrototypeSettings(UsePrototypeMode = true)] // Custom attribute inheriting NpgsqlDbContextOptionsAttribute
public class MyDbContext : DrnContext<MyDbContext> { ... }
```

### Development Environment Configurations

The framework reduces dev-setup friction by inferring configurations:

*   **Auto-Connection String**: If `postgres-password` is set and environment is Development, a connection string is generated automatically (defaulting to localhost:5432).
*   **Environment Variables**:
    *   `DrnContext_AutoMigrateDevEnvironment`: Set `true` to apply migrations at startup.
    *   `DrnContext_DevHost`, `DrnContext_DevPort`, `DrnContext_DevDatabase`: Customize the auto-generated connection details.

> [!TIP]
> Just setting `postgres-password` and mounting a volume for your DB container is usually enough to get a full persistent dev environment running.

### Global Usings

```csharp
global using DRN.Framework.EntityFramework.Context;
global using Microsoft.EntityFrameworkCore;
global using DRN.Framework.Utils.DependencyInjection;
```

---
**Semper Progressivus: Always Progressive**