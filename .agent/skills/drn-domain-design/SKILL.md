---
name: drn-domain-design
description: "Model DRN aggregates, repository boundaries, contract DTOs, mapping, and paginated APIs."
last-updated: 2026-11-22
difficulty: advanced
---

# Domain Design Patterns

## Entity Design

Use application-owned types and installed SharedKernel/EntityFramework APIs. [Resolve the package version](../overview-drn-framework/SKILL.md#documentation-and-source-references)

Use `SourceKnownEntity` for an entity and `AggregateRoot` for a repository root. Select the domain's existing `IAppId` and a unique entity byte for that partition. Annotate the concrete entity with the application-derived attribute and a member of its byte-backed enum. Keep an EF materialization constructor and enforce invariants in the domain constructor and mutation methods. Restrict setters. Use `AggregateRoot<TModel>` for a document model whose `Model` property maps to PostgreSQL `jsonb`. [SharedKernel identity](../drn-sharedkernel/SKILL.md#entity-base-class)

## EF Core Configuration

Add a `DbSet<T>` and, when needed, an `IEntityTypeConfiguration<T>` in the context's assembly and namespace or a child namespace. Prefer attributes for simple mappings. Define relationships before navigation options such as `AutoInclude()`. Let DrnContext manage internal IDs, external identity, timestamps and lifecycle events. Keep computed external identity properties out of database columns. Follow the [prototype safeguards](../drn-entityframework/SKILL.md#prototype-mode) for ephemeral development databases.

## Repository Implementation

Define `ISourceKnownRepository<TAggregate>` interfaces in Domain. Implement them in Infrastructure with `SourceKnownRepository<TContext, TAggregate>` and scoped DI. Use inherited `IEntityUtils` for identity, pagination, dates, cancellation and logging. Start custom queries from `EntitiesWithAppliedSettings()` to retain caller settings.

```csharp
public interface IUserRepository : ISourceKnownRepository<UserEntity> { }

[Scoped<IUserRepository>]
public class UserRepository(ExampleDbContext context, IEntityUtils utils)
    : SourceKnownRepository<ExampleDbContext, UserEntity>(context, utils), IUserRepository
{
    protected override IQueryable<UserEntity> EntitiesWithAppliedSettings([CallerMemberName] string? caller = null) =>
        base.EntitiesWithAppliedSettings(caller)
            .Include(u => u.Profile)
            .ThenInclude(p => p.Address);
}
```

Validate external GUIDs against the entity type and application partition at the repository boundary. Let the parser enforce the selected format. [Repository contracts](../drn-sharedkernel/SKILL.md#repository-contract)

## Contract Layer

Keep `*.Contract` dependent only on SharedKernel. Put DTOs, value models, explicit public `*Request`/`*Response` messages, shared constants, and shared application enums there. Keep entities, repository interfaces, domain services, and `[DomainOnly]` enums in Domain. Resolve the installed `DRN.Framework.SharedKernel` package's `CONVENTIONS.md` through the [version-matched framework reference procedure](../overview-drn-framework/SKILL.md#documentation-and-source-references) for folders-only Domain grouping, subdomain `_Shared` value objects, `[AllowZero]` enum rules, and canonical Domain, Subdomain, project stem, and role stem definitions (**Terminology**).

## DTO Mapping Rules

Keep internal `Dto` types derived from `Dto` in the owning Contract `(Domain, Subdomain)` plural entity folder, passing the optional entity to populate GUID identity and timestamps. Use `required` init properties for entity-specific data.

```csharp
// Contract
public class CategoryDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Name { get; init; }
}
```

In Domain, override `SourceKnownEntity.ToDto()` covariantly with `public override CategoryDto ToDto() => new(this) { Name = Name };`. Application services deriving from `SourceKnownService<TEntity, TResponse>` or `SourceKnownService<TEntity, TResponse, TCreateRequest>` implement `CreateResponseAsync` (and may override `CreateResponsesAsync` to batch async enrichment), and write-capable services also implement `CreateEntityAsync`. Select only fields needed by consumers. Preserve each mapped property's Personal and Sensitive classifications on both DTOs and Contract `*Response` projections, including when renaming fields. A metadata-only mapper can return `new(this)`. Map writable request fields through domain constructors or operations so invariants run on every path.

## API Integration

Use explicit Contract `*Request` and `*Response` types on public endpoints instead of returning, accepting, inheriting from, or nesting a `Dto` (DRNDTO005 and DRNDTO006). Route-only GETs need no empty request body, and 204 responses need no response body. Apply `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` on strict Request types to reject unexpected caller fields, and use nullable properties where presence must be tracked. Keep entity-prefixed public requests and responses beside the matching DTO (DRNDTO004).

Use paged collection endpoints by default. Accept `PaginationOptions` from the query for flat initial GET (`GET Paginate`) and `PaginationRequest` for query or POST continuation (`GET` root and `POST Paginate`). For an individual item accept a GUID and delegate response mapping through `ISourceKnownService<TEntity, TResponse>` in a service-backed `SourceKnown*Controller`. Map custom pages with `result.ToModel(...)` or `CreateResponsesAsync`. Enumerate the full dataset only when explicitly requested.

Use `EntityCreatedFilter.After`, `Before`, `Between` or `Outside` for creation dates. Boundaries default to inclusive. Keep date logic in the shared filter to preserve ID tick precision. Once mapping and registration are in place, follow the repository's migration and execution gates.
