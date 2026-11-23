---
name: drn-sharedkernel
description: "DRN domain primitives, Source-Known identity and time, repository contracts, pagination, exceptions, and classification analyzers."
last-updated: 2026-11-23
difficulty: intermediate
---

# DRN.Framework.SharedKernel

Contract and Domain projects can reference `DRN.Framework.SharedKernel` directly. Use its installed README and XML documentation for entity attributes, ID overloads and analyzer diagnostics. [Locate the package](../overview-drn-framework/SKILL.md#documentation-and-source-references)

## Entity Base Class

Derive an entity from `SourceKnownEntity` or an aggregate from `AggregateRoot`, overriding `public abstract Dto ToDto()` covariantly to return the owning Contract DTO. Use `AggregateRoot<TModel>` for a JSON model. Enforce invariants through domain operations. Use the internal long ID for persistence and the Guid externally. EF supplies identity operations on saved and queried entities. Use injected ID utilities on fresh entities. An assigned ID does not prove persistence. Created, modified and deleted domain events correspond to lifecycle transitions. [EF lifecycle](../drn-entityframework/SKILL.md#augmented-entity-behavior)

Assign each concrete, effectively non-private entity a unique byte in its application partition with a domain-derived attribute accepting a byte-backed enum. Generic abstract bases need closed concrete descendants. Keep identity members visible and unshadowed. Bind metadata and repositories to concrete types. Derived attributes forward the enum value unchanged to the base constructor.

`IAppId` declares a constant-resolvable partition from 0 through 127. Return the published constant from the getter implementing `IAppId.AppId`. DefaultApp uses 0, NexusApp 126, and TestApp 127. Define an enum-based fixture attribute for the selected test partition. The same entity byte can appear in different partitions. Collision checks include transitive project references and DRN Framework assemblies. They skip referenced `System.*`, `Microsoft.*` and external framework dependencies. See the installed analyzer documentation for opt-ins, eligibility and diagnostic IDs.

## Source-Known Identity System

Configure the epoch and minimum UTC before startup or the first ID/epoch operation. An explicit epoch requires a minimum in the same call. The minimum rounds up to a 250 ms boundary relative to the epoch. First use freezes both values even if validation fails. Historical conversion also freezes them without enforcing the generation floor. Preserve the origin across services and restarts because persisted IDs omit it. Supported IDs cover both halves of epoch zero.

### Validation Approaches

Validate external GUIDs through injected `ISourceKnownEntityIdOperations`, such as `ids.Validate<OrderEntity>(externalGuid)`, or `ISourceKnownRepository<OrderEntity>.GetEntityId(externalGuid)`. Use the application's entity type and check installed overloads. Parsed records are caller-constructible and validate stored metadata only. `Validate<TEntity>()` or `EntityTypeId(entityType, appId)` checks both identity components. Supply both even when one is zero. `ValidateId()` checks validity alone.

GUID helpers default to `ConfiguredDefault`. Implementations expose an immutable Secure or Plain default. Explicit Auto accepts both forms. Conversions reauthenticate using Auto, so enforce the required input format before conversion. Unsupported formats reject null GUIDs and empty GUID batches too. Record reads, deletes and validation accept either parsed representation. Use records only within trusted code. [Utils ID formats](../drn-utils/SKILL.md#id-generation)

## Repository Contract

Concrete aggregates use an `Entity` suffix and a matching final namespace and folder (`QA/Order/OrderEntity.cs` in `<DomainRoot>.QA.Order`). Each needs an `ISourceKnownRepository<TEntity>` interface in the same Domain assembly, namespace, and folder (`QA/Order/IOrderRepository.cs`). Compile/Link paths govern folder checks. Repositories end in `Repository`, semantic services in `Service`, and MVC controllers in `Controller` (`DRNSTRUCTURE004`/`005`, `DRNREPOSITORY003`/`004`). Abstract and open generic helpers are excluded. See the installed `CONVENTIONS.md` for scope and exceptions.

`ISourceKnownService<TEntity>` exposes `ISourceKnownRepository<TEntity> Repository` for `AggregateRoot`. `ISourceKnownService<TEntity, TResponse>` adds `CreateResponseAsync` and `CreateResponsesAsync`, and `ISourceKnownService<TEntity, TResponse, in TCreateRequest>` adds `CreateEntityAsync`. Matching `SourceKnownService` abstract bases implement them from a constructor-supplied repository, enforce input/cancellation/projection invariants on the non-overridable public methods, and delegate to protected `CreateResponseCoreAsync`, virtual sequential `CreateResponsesCoreAsync` (override to batch async enrichment), and `CreateEntityCoreAsync`.

Use GUID overloads for external IDs. Generic repository methods validate entity type and partition. Keep internal DTOs derived from `Dto` in `*.Contract` with GUID IDs, keep public HTTP contracts explicit (`*Request`/`*Response`), and keep domain mapping and invariants in Domain. See [domain design](../drn-domain-design/SKILL.md) and [repository cancellation](../drn-entityframework/SKILL.md#repository-cancellation).

### Pagination

Refresh detects another page from an extra fetched row without a total or caller hint. Null JSON page-size or cursor members use defaults. Test through serialization and real results, not only mocks.

`PaginationRequest.From()` defaults to 10 items, max 100, ascending. Bind flat `PaginationOptions` and convert with `PaginationRequest.DefaultWith(options)`. Changing size, max, or direction resets to page one with a fresh cursor. Cap max at 1,000 before comparing. Jumps advance at most 10 pages. Use `PaginateAsync` for interactive reads and `PaginateAllAsync` for streaming. Map pages with `ToModel(...)`.

## Exceptions, JSON and paths

Use `ExceptionFor` factories. Custom semantic subclasses inherit status. Direct `DrnException` subclasses default to 500. Hosting owns HTTP mapping and malicious-request aborts. [Hosting error handling](../drn-hosting/SKILL.md#error-responses)

Use `JsonConventions.DefaultOptions` (Web defaults, string enums and longs, camelCase, trailing commas, max depth 32). Use `GetPathWithinDirectory()` to reject parent and symlink traversal. The [Utils skill](../drn-utils/SKILL.md) covers app data roots.

## Attributes

Installed `CONVENTIONS.md` owns roles, dependencies, ownership, domain layouts (`DrnDomainLayout=Project|Folders`, `DrnDomain`), folders-only Domain rules (`DRNSTRUCTURE008`), Domain-role subdomains (`DRNOWNERSHIP004`), strict contracts (`DRNDTO001`..`006`, `SourceKnownEntity.ToDto()`), enum rules (`DRNENUM001`..`005`, `[DomainOnly]`, `[AllowZero]`), canonical **Terminology**, and exceptions. Installed `SCAFFOLDING.md` owns editor actions. Convention warnings stay independent of identity, classification, and authorization.

Analyzer and code-fix assemblies target `net10.0` independently of `net11.0` apps. Keep packaged DLL paths aligned with tooling targets. Allocate the next 3-digit ID per prefix, reserve retired IDs, and register IDs in `AnalyzerReleases.Unshipped.md`, contract tests, and `CONVENTION-DIAGNOSTICS.md`.

`DRNDEPENDENCYINJECTION003` requires Hosted, Application, Infra, and Utils projects to declare a public top-level nongeneric static `*Module` class with a public static nongeneric `IServiceCollection` method scanning its own assembly via `AddServicesWithAttributes()`. Domain, Contract, SharedKernel, framework, and tests are excluded.

Use [Roslyn performance guidance](../basic-agentic-development/SKILL.md#roslyn-analyzers-and-generators) for compilation-scoped reuse and cancellation checks before cache hits. Share lifetime facts across registration rules. Custom nongeneric attributes prove service binding via source-visible explicit constructor forwarding to `LifetimeAttribute`.

`DRNDEPENDENCYINJECTION002` requires one lifetime attribute bound to the exact matching `I{Name}` interface (any recognized role, generic/keyed/custom derivatives, or `HostedServiceAttribute` for `IHostedService`) on concrete Application/Infra implementations. Direct attributes override inherited ones. Failures report once under `DRNDEPENDENCYINJECTION002`. `DRNDEPENDENCYINJECTION001` still checks other service demands and key mismatches.

`DRNAUTHORIZATION001` requires explicit nonblank policies on exposed SourceKnown controller actions, including inherited ones. Anonymous access is rejected. `[NonAction]` is exempt. `IgnoreApi` is not.

Use `[Sensitive]`, `[Personal]`, or derived attributes for protected members. Mapped interface classifications cover concrete and nested captures. Scoped logging reads classified getters once. `[IgnoreLog]` excludes capture without disabling declaration checks. Use named annotated models for matching anonymous properties. `DRNCLASSIFICATION012` rejects direct captures hitting the 256 interface-property limit. `DRNCLASSIFICATION011` covers model traversal and configured paths.

- Apply direct `[Sensitive(false)]` only to reviewed built-in name matches. It cannot override positive classification or explicit URL protection.
- `DRNCLASSIFICATION003`/`006`/`008` support exact adjacent acknowledgements. `DRNCLASSIFICATION009` reports malformed comments and `DRNCLASSIFICATION010` warns on unused ones.
- `DRNCLASSIFICATION005` exempts direct EF schema column definitions and constraint selectors. Keep suppressions local.
- Register a project catalog once via EmbeddedResource or AdditionalFiles. Invalid catalogs report `DRNCLASSIFICATION007`. Backend dynamic data requires explicit classification.
