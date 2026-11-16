---
name: drn-sharedkernel
description: "DRN domain primitives, Source-Known identity and time, repository contracts, pagination, exceptions, and classification analyzers."
last-updated: 2026-11-16
difficulty: intermediate
---

# DRN.Framework.SharedKernel

Contract and Domain projects can reference `DRN.Framework.SharedKernel` directly. Use its installed README and XML documentation for entity attributes, ID overloads and analyzer diagnostics. [Locate the package](../overview-drn-framework/SKILL.md#documentation-and-source-references)

## Entity Base Class

Derive an entity from `SourceKnownEntity` or an aggregate from `AggregateRoot`. Use `AggregateRoot<TModel>` for a JSON model. Enforce invariants through domain operations. Use the internal long ID for persistence and the Guid externally. EF supplies identity operations on saved and queried entities. Use injected ID utilities on fresh entities. An assigned ID does not prove persistence. Created, modified and deleted domain events correspond to lifecycle transitions. [EF lifecycle](../drn-entityframework/SKILL.md#augmented-entity-behavior)

Assign each concrete, effectively non-private entity a unique byte in its application partition with `[EntityType<TApp>(byte)]` or a supported derived attribute. Generic abstract bases need closed concrete descendants. Keep identity members visible and unshadowed. Bind metadata and repositories to concrete types. Derived attributes forward one byte or byte-backed enum unchanged.

`IAppId` declares a constant-resolvable partition from 0 through 127. DefaultApp uses 0, NexusApp 126, and TestApp 127. Use `[TestEntityType(byte)]` for fixtures. The same entity byte can appear in different partitions. Check collisions across referenced assemblies, preserve test/aggregator opt-ins and treat abstract or effectively private types according to analyzer eligibility. Check the installed analyzer documentation for diagnostic IDs and exceptions.

## Source-Known Identity System

Configure the epoch and minimum UTC before startup or the first ID/epoch operation. An explicit epoch requires a minimum in the same call. The minimum rounds up to a 250 ms boundary relative to the epoch. First use freezes both values even if validation fails. Historical conversion also freezes them without enforcing the generation floor. Preserve the origin across services and restarts because persisted IDs omit it. Supported IDs cover both halves of epoch zero.

### Validation Approaches

Validate external GUIDs through injected `ISourceKnownEntityIdOperations`, such as `ids.Validate<Order>(externalGuid)`, or `ISourceKnownRepository<Order>.GetEntityId(externalGuid)`. Use the application's entity type and check installed overloads. Parsed records are caller-constructible and validate stored metadata only. `Validate<TEntity>()` or `EntityTypeId(entityType, appId)` checks both identity components. Supply both even when one is zero. `ValidateId()` checks validity alone.

GUID helpers default to `ConfiguredDefault`. Implementations expose an immutable Secure or Plain default. Explicit Auto accepts both forms. Conversions reauthenticate using Auto, so enforce the required input format before conversion. Unsupported formats reject null GUIDs and empty GUID batches too. Record reads, deletes and validation accept either parsed representation. Use records only within trusted code. [Utils ID formats](../drn-utils/SKILL.md#id-generation)

## Repository Contract

Concrete aggregates use an `Entity` suffix and a final namespace/folder matching the name without that suffix: `QA/Order/OrderEntity.cs` in `<DomainRoot>.QA.Order`. Each needs an `ISourceKnownRepository<TEntity>`-derived interface in the same Domain assembly, namespace and logical folder, such as `QA/Order/IOrderRepository.cs`. Use Compile/Link paths for folder validation. Repository classes/interfaces end in `Repository`, semantic services/contracts in `Service`, and MVC controllers in `Controller`. DRNCV024-027 own these checks. Abstract/open generic helpers are excluded. Other role folders keep their existing conventions. Consult CONVENTIONS.md for exact scope and intentional exceptions.

`ISourceKnownService<TEntity>` in `DRN.Framework.SharedKernel.Domain` exposes a read-only `ISourceKnownRepository<TEntity> Repository` property and requires `TEntity : AggregateRoot`.
The abstract `SourceKnownService<TEntity>` base class implements this contract with a repository supplied through its constructor.

Use GUID overloads for external IDs. Generic repository methods validate the declared entity type and partition. Expose DTOs deriving from `Dto` with GUID identifiers. Keep domain mapping and invariants in the domain layer. See [domain design](../drn-domain-design/SKILL.md) and [repository cancellation](../drn-entityframework/SKILL.md#repository-cancellation).

### Pagination

Refresh detects another page from an extra fetched row without a total or caller hint. Null JSON page-size/cursor members use defaults. Test these through serialization and real result construction, not only mocked metadata.

`PaginationRequest.From()` defaults to 10 items, maximum 100, ascending. Bind flat first-page `PaginationOptions` from a query or JSON body and convert with `PaginationRequest.DefaultWith(options)`. Changing size, effective maximum or direction resets to page one with a fresh cursor. Omitted settings carry forward. Cap the maximum at 1,000 before comparing. Use the returned `PaginationResultInfo` to navigate with first/last ID cursors. Jumps advance at most ten pages in the requested direction. Use `PaginateAsync` for interactive reads and `PaginateAllAsync` for explicitly requested streaming. Map results with `ToModel(entity => entity.ToDto())`.

## Exceptions, JSON and paths

Use the semantic `ExceptionFor` factories. A custom semantic subclass inherits its status. Direct `DrnException` subclasses default to 500 unless supplied a status. Hosting owns HTTP mapping and malicious-request abort behavior. [Hosting error handling](../drn-hosting/SKILL.md#error-responses)

Use `JsonConventions.DefaultOptions` for framework serialization, including Web defaults, string enums and longs, camelCase, trailing commas and maximum depth 32. Use `GetPathWithinDirectory()` for manifests, uploads, and file serving to reject parent and symlink traversal. The [Utils skill](../drn-utils/SKILL.md) covers application data roots.

## Attributes

The installed package's `CONVENTIONS.md` owns application roles, dependencies, ownership and diagnostic exceptions. `SCAFFOLDING.md` describes supported editor actions and their limits. Consult these guides before adopting conventions or applying generated artifacts. Convention warnings remain independent of identity, classification and authorization diagnostics.

Analyzer/code-fix assemblies target net10.0 independently of net11.0 applications. The package README owns compiler-host requirements and reload guidance. Keep packaged DLL paths aligned with tooling targets.

When adding an analyzer diagnostic, register its ID, category and default severity in the owning analyzer project's `AnalyzerReleases.Unshipped.md`. Verify the release-tracking entry alongside the descriptor and public diagnostic documentation. Missing entries fail compilation with RS2000.

DRNCV032 requires Hosted, Application, Infra and Utils projects to expose a public static nongeneric method on a public top-level nongeneric static *Module class. Accept and return IServiceCollection and directly scan the local assembly on that parameter with AddServicesWithAttributes. Reuse the composition pass for this check. Keep Domain, Contract, SharedKernel, framework and tests excluded. Do not confuse module structure with proof of runtime invocation.

For analyzer implementation changes, apply the [Roslyn performance guidance](../basic-agentic-development/SKILL.md#roslyn-analyzers-and-generators). Review new loops and semantic queries for repeated scans, allocations and compilation-scoped reuse before completion.

Share lifetime selection and binding facts across registration rules at compilation scope. Check cancellation before cache hits and publication. Custom nongeneric attributes can establish service binding through source-visible explicit constructor forwarding of typeof expressions or type-valued attribute arguments. Keep arbitrary expressions and custom key semantics outside the proof.

Avoid duplicate DRNCV012/031 reports for the same implementation/interface binding failure. Preserve DRNCV012 checks for other service demands and key mismatches. Do not claim either rule detects a missing runtime attribute scan.

DRNCV031 requires one lifetime attribute bound to the exact matching IName interface, including generic arguments, for concrete, closed Application/Infra service implementations. Interface pairing uses any recognized role; see CONVENTIONS.md for the eligibility boundary. Generic and keyed lifetime derivatives count, including custom derivatives. Eligible HostedServiceAttribute implementations bind IHostedService. Direct attributes take precedence over inherited attributes. Self-registration, other interface bindings and ambiguous lifetime declarations fail even with manual registration or unknown composition. Preserve documented service eligibility exclusions, including page models and controllers. DRNCV012 separately checks effective registration and required keys. Diagnostic examples should show Scoped, Transient and Singleton without choosing a lifetime for the user.

DRN0026 requires explicit nonblank authorization policies on exposed SourceKnown controller actions, including inherited actions. Anonymous actions are rejected. `[NonAction]` is exempt, `IgnoreApi` is not. Hosting owns runtime policies and application resource checks.

Use `[Sensitive]`, `[Personal]` or their derived attributes for protected members. Mapped interface classifications cover concrete and nested captures. Scoped logging reads classified getters once. Direct sanitization skips them. `[IgnoreLog]` excludes capture but leaves declaration analysis active. Apply these rules to fixtures too. Use named annotated models for matching anonymous properties, including nested and inferred members.

DRN0027 rejects direct property captures when the 256 interface-property scan limit prevents a complete classification decision. Remove or explicitly redact the capture, or declare classification directly on its property or type. DRN0025 remains specific to model traversal and configured paths. Name exemptions waive neither limit diagnostic.

- Apply direct `[Sensitive(false)]` only to reviewed built-in name matches. It cannot override positive classification or explicit URL protection. Other Microsoft classifications can redact at runtime without satisfying the compiler's required classification family.
- DRN0017, DRN0020 and DRN0022 support exact adjacent acknowledgements at the reported path or captured dictionary key. Preserve configured paths, declaration checks, classified values and child traversal. DRN0023 reports malformed comments and DRN0024 reports unused comments. Known safe scalar constants and direct NSubstitute verification chains have narrow exemptions. Ordinary substitute calls are checked.
- DRN0019 exempts direct EF schema column definitions and constraint selectors. Migration data, logging and lookalike APIs still require classification. Keep suppressions local. Check the analyzer shipped with the installed package before introducing a suppression.
- Register a project catalog once through EmbeddedResource or AdditionalFiles. Referenced catalogs add rules without executing dependency code. Invalid catalogs report DRN0021. Rules stay isolated per compilation. Browser and analyzer share a baseline. Backend dynamic data requires explicit classification.
