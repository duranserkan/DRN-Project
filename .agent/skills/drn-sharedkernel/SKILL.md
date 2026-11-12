---
name: drn-sharedkernel
description: "DRN domain primitives, Source-Known identity and time, repository contracts, pagination, exceptions, and classification analyzers."
last-updated: 2026-11-07
difficulty: intermediate
---

# DRN.Framework.SharedKernel

Contract and Domain projects can reference `DRN.Framework.SharedKernel` directly. Use its installed README and XML documentation for entity attributes, ID overloads and analyzer diagnostics. [Locate the package](../overview-drn-framework/SKILL.md#documentation-and-source-references)

## Entity Base Class

Derive an entity from `SourceKnownEntity` or an aggregate from `AggregateRoot`. Use `AggregateRoot<TModel>` for a JSON model. Expose domain operations that enforce invariants. Use the internal long ID for persistence and the Guid for external identity. EF assigns internal IDs during tracking, with a save-time fallback, and attaches ID operations during materialization or saving. Use injected ID utilities on fresh entities. An initialized ID or pending insert does not establish persistence. Created, modified and deleted domain events correspond to lifecycle transitions.

Assign each concrete, effectively non-private entity a unique entity byte in its application partition using `[EntityType<TApp>(byte)]` or a supported derived attribute. Use a closed concrete descendant of a generic abstract base. Keep identity members visible and unshadowed. Use concrete types for metadata and repository bindings. Derived attributes take one byte or byte-backed enum forwarded unchanged to the framework constructor.

`IAppId` declares a constant-resolvable partition from 0 through 127. DefaultApp uses 0, NexusApp 126, and TestApp 127. Use `[TestEntityType(byte)]` for fixtures. The same entity byte can appear in different partitions. Check collisions across referenced assemblies, preserve test/aggregator opt-ins and treat abstract or effectively private types according to analyzer eligibility. Check the installed analyzer documentation for diagnostic IDs and exceptions.

## Source-Known Identity System

Configure the epoch and minimum UTC before startup or the first ID/epoch operation. An explicit epoch requires a minimum in the same call. The minimum rounds up to a 250 ms boundary relative to the epoch. First use freezes both values even if validation fails. Historical conversion also freezes them without enforcing the generation floor. Preserve the origin across services and restarts because persisted IDs omit it. Supported IDs cover both halves of epoch zero.

### Validation Approaches

Validate an external GUID with `ids.Validate<Order>(externalGuid)` through the injected ID utility or `repository.GetEntityId(externalGuid)` through an `ISourceKnownRepository<Order>`. Use the application's entity type. Check the installed overload before passing a format argument.

Validate externally supplied GUIDs through `ISourceKnownEntityIdOperations`. A parsed record can be constructed by application code. Its validation checks stored metadata only. Use `Validate<TEntity>()` or an explicit `EntityTypeId(entityType, appId)` to verify both components. `ValidateId()` checks validity alone. Supply both identity components even when one is zero.

GUID helpers default to `ConfiguredDefault`. Implementations expose an immutable Secure or Plain default. Explicit Auto accepts both forms. Conversions reauthenticate using Auto, so enforce the required input format before conversion. Unsupported formats reject null GUIDs and empty GUID batches too. Record reads, deletes and validation accept either parsed representation. Use records only within trusted code. [Utils ID formats](../drn-utils/SKILL.md#id-generation)

## Repository Contract

Use GUID overloads for external IDs. Generic repository methods validate the declared entity type and partition. Expose DTOs deriving from `Dto` with GUID identifiers. Keep domain mapping and invariants in the domain layer. See [domain design](../drn-domain-design/SKILL.md) and [repository cancellation](../drn-entityframework/SKILL.md#repository-cancellation).

### Pagination

`PaginationRequest.From()` defaults to 10 items, maximum 100, ascending. Bind flat first-page `PaginationOptions` from a query or JSON body and convert with `PaginationRequest.DefaultWith(options)`. Changing size, effective maximum or direction resets to page one with a fresh cursor. Omitted settings carry forward. Cap the maximum at 1,000 before comparing. Use the returned `PaginationResultInfo` to navigate with first/last ID cursors. Jumps advance at most ten pages in the requested direction. Use `PaginateAsync` for interactive reads and `PaginateAllAsync` for explicitly requested streaming. Map results with `ToModel(entity => entity.ToDto())`.

## Exceptions, JSON and paths

Use the semantic `ExceptionFor` factories. A custom semantic subclass inherits its status. Direct `DrnException` subclasses default to 500 unless supplied a status. Hosting owns HTTP mapping and malicious-request abort behavior. [Hosting error handling](../drn-hosting/SKILL.md#error-responses)

Use `JsonConventions.DefaultOptions` for framework serialization, including Web defaults, string enums and longs, camelCase, trailing commas and maximum depth 32. Use `GetPathWithinDirectory()` for manifests, uploads, and file serving to reject parent and symlink traversal. The [Utils skill](../drn-utils/SKILL.md) covers application data roots.

## Attributes

Use `[Sensitive]`, `[Personal]` or their derived attributes for protected members. Mapped interface classifications cover concrete and nested captures. Scoped logging reads classified getters once. Direct sanitization skips them. `[IgnoreLog]` excludes capture but leaves declaration analysis active. Apply these rules to fixtures too. Use named annotated models for matching anonymous properties, including nested and inferred members.

- Apply direct `[Sensitive(false)]` only to reviewed built-in name matches. It cannot override positive classification or explicit URL protection. Other Microsoft classifications can redact at runtime without satisfying the compiler's required classification family.
- DRN0017, DRN0020 and DRN0022 support exact adjacent acknowledgements at the reported path or captured dictionary key. Preserve configured paths, declaration checks, classified values and child traversal. DRN0023 reports malformed comments and DRN0024 reports unused comments. Known safe scalar constants and direct NSubstitute verification chains have narrow exemptions. Ordinary substitute calls are checked.
- DRN0019 exempts direct EF schema column definitions and constraint selectors. Migration data, logging and lookalike APIs still require classification. Keep suppressions local. Check the analyzer shipped with the installed package before introducing a suppression.
- Register a project catalog once through EmbeddedResource or AdditionalFiles. Inherit additive reference-assembly metadata without executing dependency code. Invalid catalogs report DRN0021. Keep rules isolated per compilation. The browser and analyzer share a baseline. Backend runtime requires explicit classification of dynamic data.
