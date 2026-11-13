---
name: drn-domain-design
description: "Model DRN aggregates, repository boundaries, contract DTOs, mapping, and paginated APIs."
last-updated: 2026-11-13
difficulty: advanced
---

# Domain Design Patterns

## Entity Design

Use application-owned types and installed SharedKernel/EntityFramework APIs. [Resolve the package version](../overview-drn-framework/SKILL.md#documentation-and-source-references)

Use `SourceKnownEntity` for an entity and `AggregateRoot` for a repository root. Select the domain's existing `IAppId` and a unique entity byte for that partition. Annotate the concrete entity with `[EntityType<TApp>(byte)]` or the application-derived attribute. Keep an EF materialization constructor and enforce invariants in the domain constructor and mutation methods. Restrict setters. Use `AggregateRoot<TModel>` for a document model whose `Model` property maps to PostgreSQL `jsonb`. [SharedKernel identity](../drn-sharedkernel/SKILL.md#entity-base-class)

## EF Core Configuration

Add a `DbSet<T>` and, when needed, an `IEntityTypeConfiguration<T>` in the context's assembly and namespace or a child namespace. Prefer attributes for simple mappings. Define relationships before navigation options such as `AutoInclude()`. Let DrnContext manage internal IDs, external identity, timestamps and lifecycle events. Keep computed external identity properties out of database columns. Follow the [prototype safeguards](../drn-entityframework/SKILL.md#prototype-mode) for ephemeral development databases.

## Repository Implementation

Define `ISourceKnownRepository<TAggregate>` interfaces in Domain. Implement them in Infrastructure with `SourceKnownRepository<TContext, TAggregate>` and scoped DI. Use inherited `IEntityUtils` for identity, pagination, dates, cancellation and logging. Start custom queries from `EntitiesWithAppliedSettings()` to retain caller settings.

```csharp
public interface IUserRepository : ISourceKnownRepository<User> { }

[Scoped<IUserRepository>]
public class UserRepository(ExampleDbContext context, IEntityUtils utils)
    : SourceKnownRepository<ExampleDbContext, User>(context, utils), IUserRepository
{
    protected override IQueryable<User> EntitiesWithAppliedSettings([CallerMemberName] string? caller = null) =>
        base.EntitiesWithAppliedSettings(caller)
            .Include(u => u.Profile)
            .ThenInclude(p => p.Address);
}
```

Validate external GUIDs against the entity type and application partition at the repository boundary. Let the parser enforce the selected format. [Repository contracts](../drn-sharedkernel/SKILL.md#repository-contract)

## Contract Layer

Keep `*.Contract` dependent only on SharedKernel. Put DTOs, value models, shared constants and enums consumed by both entities and DTOs there. Keep entities, repository interfaces, domain services and internal entity-type enums in Domain. Review exposure and validation requirements before sharing an enum across that boundary.

## DTO Mapping Rules

Expose GUIDs and DTOs at API boundaries. Derive from `Dto` and pass the optional entity to its constructor to map public identity and timestamps. Use `required` init properties with defaults for entity-specific data. Keep paging metadata on the result.

```csharp
// Contract
public class CategoryDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Name { get; init; } = string.Empty;
}
```

In Domain, map out with `CategoryDto ToDto() => new(this) { Name = Name };` on the entity. Move mapping to a domain extension class if it crowds out domain behavior. Map back through a Domain extension that calls the entity's constructor, such as `new Category(dto.Name)`, so validation runs on every path.

## API Integration

Use paged collection endpoints by default. Accept `PaginationRequest` from the query for the initial GET and `PaginationResultInfo` for subsequent GET-query or POST-body navigation. For an individual item accept a GUID and map the entity to its DTO. Map page items with `result.ToModel(entity => entity.ToDto())`. Enumerate the full dataset only when explicitly requested.

Use `EntityCreatedFilter.After`, `Before`, `Between` or `Outside` for creation dates. Boundaries default to inclusive. Keep date logic in the shared filter to preserve ID tick precision. Once mapping and registration are in place, follow the repository's migration and execution gates.
