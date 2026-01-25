---
name: drn-entityframework
description: DRN.Framework.EntityFramework - DrnContext base class, automatic migration application, entity tracking with domain events, NpgsqlDbContextOptions for database configuration, and repository implementations. Essential for database operations, migrations, and data persistence. Keywords: drncontext, ef-core, entity-framework, migrations, database, postgresql, npgsql, repository-implementation, entity-tracking, domain-events, dbcontext-configuration
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
// On Add
entity.MarkAsCreated();   // Sets CreatedAt, ModifiedAt, adds event

// On Update
entity.MarkAsModified();  // Sets ModifiedAt, adds event

// On Delete
entity.MarkAsDeleted();   // Adds event
```

Domain events are collected and can be published after `SaveChangesAsync()`.

## Augmented Entity Behavior

`DrnContext` augments entities during `OnModelCreating` and runtime:

### 1. Auto-ID Generation
Entities with `Id = 0` are automatically assigned a unique Source-Known ID via `SourceKnownIdValueGenerator`:
- Uses `ISourceKnownIdUtils.Next<TEntity>()` to generate a collision-free `long` ID.
- Configured via `HasValueGenerator<SourceKnownIdValueGenerator>()`.
- **Note**: `EntityId` (Guid) and `EntityIdSource` are ignored by EF as they are computed properties.

### 2. JSON Models
Entities implementing `IEntityWithModel<TModel>` have their `.Model` property automatically mapped to a `jsonb` column:
```csharp
entityTypeBuilder.OwnsOne(ownedType, "Model", builder => builder.ToJson());
```

### 3. Startup Validation
`[DrnContextServiceRegistration]` attribute validates that all registered entities have a valid `[EntityType]` attribute, preventing startup if IDs are missing or duplicated.


---

## Connection String Conventions

### Production

```json
{
  "ConnectionStrings": {
    "QAContext": "Host=server;Database=qa;Username=user;Password=pass"
  }
}
```

### Development Auto-Generation

When no connection string is provided in Development:

```json
{
  "Environment": "Development",
  "postgres-password": "dev-password"
}
```

Auto-generates: `Host=postgresql;Port=5432;Database=drnDb;Username=postgres;Password=dev-password`

### Development Configuration Keys

| Key | Default | Purpose |
|-----|---------|---------|
| `DrnContext_DevHost` | `postgresql` | Database host |
| `DrnContext_DevPort` | `5432` | Database port |
| `DrnContext_DevUsername` | `postgres` | Database user |
| `DrnContext_DevDatabase` | `drnDb` | Database name |
| `DrnContext_AutoMigrateDevEnvironment` | `false` | Auto-apply migrations |

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

---

## Service Registration

### HasDrnContextServiceCollectionModuleAttribute

```csharp
[HasDrnContextServiceCollectionModule]
public abstract class DrnContext<TContext> : DbContext, 
    IDesignTimeDbContextFactory<TContext>, 
    IDesignTimeServices 
    where TContext : DbContext, new()
```

### Post-Startup Validation

After service validation, if `AutoMigrateDevEnvironment` is enabled:
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
    await context.ContainerContext.StartPostgresAndApplyMigrationsAsync();
    
    var qaContext = context.GetRequiredService<QAContext>();
    // Ready to test
}
```

---

## Global Usings

```csharp
global using DRN.Framework.EntityFramework.Context;
global using Microsoft.EntityFrameworkCore;
global using DRN.Framework.Utils.DependencyInjection;
```

```

## SourceKnownRepository Implementation

`SourceKnownRepository<TContext, TEntity>` implements `ISourceKnownRepository<TEntity>`:

- Uses `IEntityUtils` for ID validation and parsing.
- Implements `GetAsync`, `GetOrDefaultAsync` using `SourceKnownEntityId` validation.
- Provides automatic `ScopedLog` measurements for operations.
- See [drn-sharedkernel.md](../drn-sharedkernel/SKILL.md) for the interface definition.

---

## Related Skills

- [drn-domain-design.md](../drn-domain-design/SKILL.md) - Domain & Repository patterns
- [drn-sharedkernel.md](../drn-sharedkernel/SKILL.md) - Entity base classes
- [drn-utils.md](../drn-utils/SKILL.md) - DI and configuration
- [drn-testing.md](../drn-testing/SKILL.md) - ContainerContext
- [test-integration.md](../test-integration/SKILL.md) - Database testing
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Domain modeling

---
