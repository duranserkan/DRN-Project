# SourceKnown editable assistance

Use selected artifacts to add a subdomain or aggregate without changing identity, storage or authorization by accident. Configuration classes, custom repositories and application services are optional. Attribute-only entity mapping is normal.

The IDE offers bounded configuration, repository-pair, selected-property DTO and selected-operation service refactorings. Actions preview selected files together and decline outdated previews. Plan-only actions help review aggregate/context and controller changes that need more input or unsupported project edits. Full source/project transactions require host support. These snippets provide a manual review fallback. The guarded DRNPERSISTENCE001 constructor fix is separate. Templates run no scripts, generators or database/package operations.

## Record the requested change

Before producing files, record these explicit selections:

| Input | Required decision |
|---|---|
| Application | Application root, loaded projects, project roles and namespace roots. |
| Ownership | Subdomain and optional feature. Technical aliases need a reason. |
| Aggregate | Existing exact type, or user-supplied identity allocation plus business constructor/invariants for a new type. Never choose the next local EntityType byte. |
| Persistence | Exact target context and established inclusion. Multiple same-subdomain contexts are supported. Cross-owner reuse needs a separate narrow DRNOWNERSHIP002 exception. |
| Artifacts | Select context, configuration, repository contract/implementation, actual use-case service, DTO/request, controller and destination metadata individually. |
| Service | Requested use case, repository binding, service contract, lifetime and implemented business operation. No empty service solely to connect layers. |
| Transport | Exact routes, operations, request/response fields, mapping, creation/deletion behavior, named policies, authentication transport and resource-authorization rules. |

Infer only unambiguous facts. Missing required choices keep the selected transaction unapplied. Do not invent business logic, relationships, credentials, settings, policies or throw-only endpoints.

## Preview and apply contract

The preview lists every new file and existing-file edit, exact destination paths, symbols, project metadata and preimage content. Include all inherited HTTP actions, not only methods written by the template. Record unresolved choices explicitly.

Before application, verify:

1. Every target is within the selected project boundary. Reject traversal, external destinations and symlink escapes. Check case-insensitive as well as exact path collisions.
2. Existing files, symbols, members, partial declarations and linked documents do not conflict. Generated leaf names must not collide with types or namespaces, including case variants. Every namespace segment must be free of a same-parent type collision. Never overwrite a colliding file or append a duplicate project item.
3. Configuration discovery is valid for every affected context. Moving or adding a shared mapping must not silently alter another context's scan.
4. All previewed preimages still match. Any intervening source, project or reference change invalidates the preview.
5. The host can apply all selected source and project edits atomically. If it cannot include project metadata, retain the reviewable plan and apply nothing through that action.

Cancellation before application leaves the workspace unchanged. A repeated operation is a no-op or reports an existing matching artifact. It never duplicates declarations. No Fix All is available for identity, membership, ownership, policy or multi-file scaffold decisions. A manual template plan requires the same review and conflict checks. It does not claim atomic workspace support that the host has not demonstrated.

Supplied convention input must pass the shared version, size, record-count and data-only syntax checks described in [conventions](CONVENTIONS.md#declared-inputs). Generation also validates structural field values, including logical paths and reference provenance. Invalid or unsupported input prevents default-path generation. Identical records are idempotent, while contradictory unique-target records are rejected. These checks and symbol preflight run again before applying the solution change. Semantic relationships and exception target/fingerprint validity remain analyzer-owned. Refactorings do not use exceptions as generation authority.

## Context and tracked destinations

Select an existing Domain aggregate identifier to request **Create aggregate review plan only**. Eligible Infra projects that reference the selected Domain project and their existing contexts are separate choices. Project-name prefixes do not restrict selection. Without an existing context in that owner area, the action explicitly proposes the conventional owner-plus-Context name. Context aliases need a reasoned declaration. The action adds only the Markdown `AdditionalFiles` review plan, which records current source fingerprints, existing identity declarations, known inputs and proposed destinations. It creates no C# context, membership, ID, reference or project item. Reconcile the plan with the required business and project-system decisions before implementation.

This example assumes the explicitly selected application is `Shop`, subdomain Sales, Infra namespace `Shop.Infra`, and new context `SalesContext`. Replace these values with the reviewed selection. Do not apply it to rename an existing persisted context.

File `Shop.Infra/Sales/SalesContext.cs`:

```csharp
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.EntityFramework.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Shop.Infra.Sales;

public class SalesContextNpgsqlDbContextOptionsAttribute : NpgsqlDbContextOptionsAttribute;

[SalesContextNpgsqlDbContextOptions]
public class SalesContext : DrnContext<SalesContext>
{
    public SalesContext() : base(null) { }

    public SalesContext(DbContextOptions<SalesContext> options) : base(options) { }
}
```

The existing DRN base supplies its normal convention/default path. It is an example, not a mandatory base class. Enabled application conventions require concrete application contexts to implement `IDrnContext`, including inherited implementations. The interface alone is not proof of runtime safeguards. Concrete DRN contexts must directly apply a custom options attribute. Prefer a name such as `SalesContextNpgsqlDbContextOptionsAttribute`. Add configuration overrides only for actual selected values.

The conventional `Sales/Repositories` and `Sales/Migrations` destinations need no project entry. An explicit declaration remains supported when needed to resolve ambiguity:

```xml
<ItemGroup>
  <DrnConventionContext Include="Shop.Infra.Sales.SalesContext"
                        Repositories="Sales/Repositories"
                        Migrations="Sales/Migrations" />
</ItemGroup>
```

Project-name tokens supply roles without custom properties or an application-root identity. Reuse existing context items instead of duplicating them. Infra must directly reference at least one Domain project. The referenced Domain projects collectively supply populated subdomain namespaces matching concrete Infra contexts and repositories. Alternate destinations require explicit metadata and a Reason. Do not create fake files to retain empty directories. EF tooling remains the only owner of generated migrations. A context in `Sales.Read` can use `Sales/Read/Repositories` and `Sales/Read/Migrations`. Same-owner read/write membership is valid, but runtime read-only behavior is not inferred.

## Optional entity configuration

The user must select the existing entity and target context. This example assumes `Shop.Domain.Sales.Order.OrderEntity` already exists and its mapping is attribute-based. File `Shop.Infra/Sales/Configurations/OrderConfig.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Sales.Order;

namespace Shop.Infra.Sales.Configurations;

public sealed class OrderConfig : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> builder)
    {
    }
}
```

The deliberately empty body is the explicitly requested editable configuration starting point. It is not a model-correctness claim or required boilerplate. Preserve all existing annotations and Fluent bodies. Do not introduce inferred keys, relationships, column types or delete behavior.

Confirm the selected context's real discovery path before adding this file. The action recognizes DRN's exact/child default scan, exact base forwarding, and straight-line local-assembly scans using a constant namespace equality predicate. For supported shared scans, the title names every affected context and all must have positive membership evidence. Unknown local or dependent-project discovery prevents the offer. Arbitrary predicates, external scans and custom destinations still need separate review. The lightbulb is a refactoring on valid code, with no fabricated missing-configuration warning.

Contexts in the global namespace are not configuration-generation candidates.

An intended association can be declared explicitly:

```xml
<DrnConventionConfiguration Include="Shop.Infra.Sales.Configurations.OrderConfig"
                            Context="Shop.Infra.Sales.SalesContext" />
```

## Optional repository

On an existing Domain aggregate, the Add scoped repository pair action offers each eligible Infra context separately through the existing project-reference graph. It previews the Domain interface and Infra implementation together. The Infra project must already reference the selected Domain project and include the entity. A conventional Repositories destination is inferred when the context namespace and source home agree. Explicit destinations and intentional context aliases remain validated. Naming prefixes do not have to match. Existing repository bindings, conflicting ownership, unsafe destinations or unsupported discovery prevent the offer. It adds no project reference or destination metadata. The selected action requests scoped registration and names both files in its title.

Choose **Add scoped repository pair and entity configuration** to request those two files plus the optional configuration in one solution change. Its title names the selected context and all three paths. The combined action is offered only when configuration discovery identifies that same single context. A shared configuration scan needs the separate configuration action with its all-context preview. An existing mapping or configuration collision prevents the combined action while an independently valid repository pair can remain available. Cancellation or a changed workspace invalidates the entire combined action. It never applies only part of the three-file selection.

Reasoned entity or context ownership overrides do not authorize generation when the new interface or implementation would inherit a different owner from its namespace.

Only create a custom repository when the selected use case needs that contract. This example assumes the existing `OrderEntity` aggregate is included in `SalesContext` and the requested service type is `IOrderRepository`.

File `Shop.Domain/Sales/Order/IOrderRepository.cs`:

```csharp
using DRN.Framework.SharedKernel.Domain.Repository;

namespace Shop.Domain.Sales.Order;

public interface IOrderRepository : ISourceKnownRepository<OrderEntity>;
```

File `Shop.Infra/Sales/Repositories/OrderRepository.cs`:

```csharp
using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using Shop.Domain.Sales.Order;

namespace Shop.Infra.Sales.Repositories;

[Scoped<IOrderRepository>]
public class OrderRepository(SalesContext context, IEntityUtils utils)
    : SourceKnownRepository<SalesContext, OrderEntity>(context, utils), IOrderRepository;
```

Preserve the application's existing registration path. Attribute registration requires the application's real assembly composition path. Do not add duplicate manual registration. Do not guess query filters, tracking settings, keyed selection or a new transaction boundary. Verify effective DI when execution is authorized.

## Optional service, DTO and request

A requested repository-backed service may derive from `SourceKnownService<TEntity>` or implement `ISourceKnownService<TEntity>`. Supply the actual requested operation and domain behavior before writing it. If the request only needs repository access, no service is required. A service skeleton with a hidden TODO or `NotImplementedException` is not an acceptable output.

For an explicitly requested forwarding use case, select one complete method declaration on an existing Domain repository interface. The Add forwarding service action offers eligible loaded Application projects and separate scoped/unregistered choices. It forwards exactly the selected method to the repository, and asynchronous (`Task`/`ValueTask`) methods require a `CancellationToken` parameter to qualify. It adds no business validation, query, write behavior or extra operations. Unsupported signatures, unavailable types, ownership conflicts or a missing existing Domain reference prevent the offer. Use this only when forwarding that existing operation is the intended use case. More involved orchestration still requires the real business implementation.

For DTO/request assistance, list each external field, its type, classification, validation owner and mapping source first. Public DTOs derive from `Dto` and expose Guid identity. Preserve CreatedAt/ModifiedAt mapping. Put public contracts in Contract and entity mapping in an allowed Domain/Application location. Do not introduce a Contract-to-Domain reference just to simplify mapping.

To request the bounded DTO action, select the complete declarations of the public readable scalar properties to expose on an existing Domain entity. The action offers a loaded Contract project only when the selected Domain project already references it, regardless of naming prefix. Preview includes a Contract DTO and a Domain mapping extension. The DTO constructor forwards the source entity to Dto, preserving identity and timestamps. The mapper initializes every selected field. Nonnullable reference fields are required init properties, without invented defaults. Known classification attributes are retained. Internal numeric *Id properties (long or long?), partial properties, unsupported types or unprojected classification metadata prevent the action. This does not generate a creation request, validation, endpoint or authorization behavior.

Enum properties require an accessible matching enum in the Contract project. Enums nested inside generic containing types are declined.

For example, only after the user selects a public `Number` field, a Contract DTO may be:

```csharp
using DRN.Framework.SharedKernel.Domain;

namespace Shop.Contract.Sales.Order;

public sealed class OrderDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Number { get; init; }
}
```

The operation's real mapping must populate Number and inherited identity/timestamps from the established entity. This guide does not invent that entity's business members. A creation request requires explicit allowed fields and real domain construction/validation. Existing authorization and input-validation rules remain active.

## Optional controller and exposure inventory

Ordinary MVC controllers and Razor Pages are supported without SourceKnown controller inheritance. Their architecture and public persistence-exposure checks remain active. SourceKnown controller actions retain DRNAUTHORIZATION001 authorization enforcement.

On an existing SourceKnown controller identifier, **Create controller review plan only** adds one Markdown review plan. It inventories inherited SourceKnown and application methods, route/policy/NonAction declarations, source fingerprints and unresolved exposure/mapping/resource decisions. Exact MVC NonAction helpers are summarized. It changes no runnable controller, route, policy, reference or project item. The plan is not a runtime authorization proof or a substitute for explicit choices.

Both review-plan actions require an IDE that supports adding Markdown review plans. They decline colliding destinations, linked or generated selected source and outdated previews. The aggregate plan also requires supported ownership. They add only the selected Markdown plan, with no executable scaffolding or project metadata. Repeating the action at the same existing plan destination declines instead of overwriting it. A saved plan is a review snapshot. Recheck its source fingerprints and decisions before any later implementation.

Do not emit a controller until every requested operation has a real mapping, named policy and resource-authorization decision. Inheriting a controller base can expose operations immediately.

| Selected base | Inherited HTTP surface to review |
|---|---|
| `SourceKnownControllerBase<TEntity,TDto>` | No HTTP actions. Own each requested operation explicitly. |
| `SourceKnownReadOnlyController<TEntity,TDto>` | GetAsync, GetManyAsync, AnyAsync, CountAsync, PaginateAsync, PaginateWithOptionsAsync, PaginateWithBodyAsync. POST lookup/pagination are still reads. |
| `SourceKnownController<TEntity,TDto,TCreateRequest>` | All read actions plus PostAsync and DeleteAsync. |
| `SourceKnownWriteOnlyController<TEntity,TDto,TCreateRequest>` | PostAsync and DeleteAsync. Inherited reads are sealed NonAction overrides. |

Recheck the installed base's full inheritance chain when creating the preview. For each exposed action record route, verb, selected/enabled state, named policy, resource scope, input/output mapping and cancellation behavior. Explicitly disable unselected overridable actions with `[NonAction]`. `IgnoreApi` hides documentation but does not disable routing. Do not use `AllowAnonymous` as a fallback.

Inherited DeleteAsync uses repository direct-delete semantics and does not invoke entity deletion lifecycle hooks. The user's required deletion behavior must be compatible before selecting it. Creation requires a real CreateEntityAsync implementation. Repository filters must enforce the application's data visibility. A named policy alone does not establish per-resource access.

Controller review plans leave application operations, policies and resource decisions for explicit selection. They do not generate throw-only endpoints. Resolve those decisions before implementing the plan.

## Compatibility and evidence

Keep context CLR names, connection keys, schemas, migration history, entity IDs, authentication behavior and public routes stable. Namespace normalization of existing users requires a full consumer inventory and model/snapshot compatibility evidence. Do not hand-edit historical migrations/designers to make the diff appear compatible.

Validate compiler and package behavior in the intended host. Review IDE previews and multi-project application before adoption. Static assistance does not prove model equivalence or runtime authorization. See [conventions](CONVENTIONS.md) for supported rule coverage and exception channels.
