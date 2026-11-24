Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.11.0

### Security

*   **JSON integer errors**: Invalid `long` and `long?` values produce a conversion error that omits the submitted value.

### Breaking Changes

*   **Target framework**: Requires .NET 11. Analyzer and code-fix assemblies target .NET 10 for IDE host compatibility and require Roslyn 5.12 APIs.
*   **Diagnostic IDs**: Replace `DRN0001` through `DRN0014` with `DRNENTITY001` through `DRNENTITY014` (keeping the same numbers) in `.editorconfig`, `NoWarn`, `WarningsAsErrors`, `#pragma warning`, and `SuppressMessage`.
*   **Entity type declarations**: `DRNENTITY008` now requires a domain attribute with a byte-backed enum parameter instead of direct `[EntityType<TApp>(n)]`, `[TestEntityType(n)]`, or raw-byte wrappers. Assign each enum member its existing byte value, keep the same `TApp` partition to preserve stored IDs, and rebuild referenced domain assemblies that still use byte-based annotations.
*   **Entity `ToDto()` contract**: Added `public abstract Dto ToDto()` on `SourceKnownEntity`. Override `ToDto()` covariantly on every concrete entity to return its Contract DTO via `new(this)`.
*   **Partition producers**: `DRNENTITY006` and `DRNENTITY007` now also check concrete `IAppId` implementations in projects without entities. Publish a constant value from 0 through 127 and return it from `IAppId.AppId`.
*   **Data classification analyzers**: `DRNCLASSIFICATION001` through `DRNCLASSIFICATION012` run transitively on public readable instance properties, anonymous projections, dictionary captures, `IScopedLog`, and `ILogger` calls. Annotate matching properties with `[Personal]`, `[Sensitive]`, or `[Sensitive(false)]`, move matching anonymous properties to named annotated models, and classify dictionary keys or redact sensitive logger values. See the [diagnostic guide](https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Utils/Logging/Diagnostics/README.md).
*   **Application convention analyzers**: Convention rules (`DRNARCHITECTURE*`, `DRNCONVENTIONINPUT*`, `DRNCONVENTIONEXCEPTION*`, `DRNDTO*`, `DRNENUM*`, `DRNOWNERSHIP*`, `DRNSTRUCTURE*`, `DRNPERSISTENCE*`, `DRNREPOSITORY*`, `DRNDEPENDENCYINJECTION*`) activate by default as warnings on recognized project-name roles and fail `TreatWarningsAsErrors` builds. Configure `.editorconfig` severities or narrow `DrnConventionException` items for intentional exceptions. See `CONVENTIONS.md` and `CONVENTION-DIAGNOSTICS.md`.

### New Features

*   **SourceKnown application conventions**: Added project-role dependency, domain/subdomain ownership (`<DrnDomainLayout>`, `<DrnDomain>`, `[SourceKnownSubdomain]`), folder and naming structure, persistence (`IDrnContext`, aggregate `DbSet`s, custom context options), repository, DI module and lifetime binding, DTO and public-contract (`*Request`/`*Response`), and enum (`[DomainOnly]`, `[AllowZero]`) diagnostics, plus guarded code fixes, refactorings, and review plans. See `CONVENTIONS.md` and `SCAFFOLDING.md`.
*   **Source-Known services**: Added `ISourceKnownService<TEntity>`, `ISourceKnownService<TEntity, TResponse>`, `ISourceKnownService<TEntity, TResponse, in TCreateRequest>`, and matching `SourceKnownService` abstract bases enforcing argument, cancellation, and projection invariants around protected `CreateResponseCoreAsync`, virtual batched `CreateResponsesCoreAsync`, and `CreateEntityCoreAsync` hooks.
*   **Source-Known controller authorization**: Added `DRNAUTHORIZATION001`, requiring explicit nonblank named `[Authorize(Policy = "...")]` policies on exposed Source-Known controller actions and rejecting `[AllowAnonymous]`.
*   **Data classification and exemptions**: Added inheritable `[Sensitive]` and `[Personal]` attributes, `[Sensitive(false)]` false-positive opt-outs, project `data-classification-rules.json` (and `diagnostic-rules.json`) catalogs, `DiagnosticNameExemption` comments, and classification code fixes.
*   **Pagination options**: Added `PaginationOptions` and `PaginationRequest.DefaultWith(options)` for flat first-page query or JSON binding.

### Bug Fixes

*   **Public contracts and scaffolding**. DRNDTO003 checks classification on projected collection elements and on Contract child models constructed in helpers or locals. DRNDTO005 reports each exposed DTO on its declaring member or endpoint. DRNARCHITECTURE002 checks persistence interfaces in Contract type declarations. Scaffolding rejects contradictory producer metadata and requires a forwarded repository operation and its aggregate to share a Domain assembly.
*   **Entity diagnostics**: `DRNENTITY009` and `DRNENTITY010` now check converted constants passed through nullable numeric casts. `DRNENTITY013` reports an invalid repository type initializer once. `DRNENTITY006` verifies `IAppId.AppId` getters against published constants when source is available, and `DRNENTITY002` checks collisions across transitive project references while skipping `System.*`, `Microsoft.*`, and external framework dependencies. Incomplete primary-constructor base lists no longer cause analyzer failures on derived entity attributes.
*   **Packaged analyzer loading**: Duplicate packaged analyzer entries are removed before compilation.
*   **Pagination**: Refresh results detect a next page from an extra fetched row without a known total or caller hint. Null JSON `PageSize` and `PageCursor` members now use defaults instead of throwing.
*   **Path containment**: `GetPathWithinDirectory()` accepts missing child files and directories while checking existing ancestors for symbolic links.

## Version 0.10.0

### Breaking Changes

*   **Entity ID Format Contracts**: GUID operations add `SourceKnownEntityIdFormat` (`ConfiguredDefault = 0`, `Secure = 1`, `Plain = 2`, `Auto = 3`), defaulting to `ConfiguredDefault`. Custom `ISourceKnownEntityIdOperations` implementations must expose an immutable Secure/Plain `DefaultFormat`. Rebuild consumers and update GUID method signatures and method-group bindings. Format is enforced when parsing GUIDs; parsed-record validation takes no format argument and does not reauthenticate GUIDs. Null GUIDs remain supported; undefined formats throw even for null GUIDs and empty GUID batches.
*   **Explicit Expected Identity**: Replace byte-only identity arguments with `new EntityTypeId(entityType, expectedAppId)`: `Generate(long, byte)` becomes `Generate(long, EntityTypeId)`, `HasSameEntityType(byte)` becomes `HasSameEntityTypeId(EntityTypeId)`, and `Validate(byte)` becomes `Validate(EntityTypeId)`. Domain `GetEntityId` accepts this composite identity or a generic entity type. Validation checks both components; `ValidateId()` and the domain GUID helper's boolean option remain validity-only.
*   **Generation Time Policy**: New IDs use a minimum of `2026-09-09T00:00:00Z` and an epoch of `2025-01-01T00:00:00Z` by default. Configure overrides through `SourceKnownGenerationTime.Initialize(minimumUtc, defaultEpoch)` before startup or first ID/epoch use; an explicit epoch requires a minimum. Both values then freeze. Historical reads remain exempt from the floor, and every service and restart using a dataset must retain its origin. See [configuration and limits](README.md#trusted-minimum-generation-time).
*   **Entity Partitions and Analyzers**: Annotate concrete, effectively non-private entities with `[EntityType<TApp>(byte)]` or a supported derived attribute; entity type values must be unique per AppId. Derived attributes must forward one byte or byte-backed enum argument unchanged. Transitive analyzers validate declarations/partitions and require `<AllowMultipleAppIds>true</AllowMultipleAppIds>` for production projects combining partitions. Invalid constant partitions/formats and abstract metadata/repository bindings are errors; duplicate names, hidden identity members, and concrete generic entities produce warnings. Generic forwarding, runtime-instance overloads, and dynamic values remain supported. See [diagnostics and migration requirements](README.md#compile-time-roslyn-analyzers).
*   **Entity Operation Initialization**: `DRN0014` rejects `GetEntityId`, `ToSecure`, and `ToPlain` on provably fresh entities, including aliases and constructor `this`. Use injected ID utilities before initialization. Unknown effects and potentially null inputs remain runtime concerns; initialization does not prove persistence.

### New Features

*   **Application Partitioning**: Added `IAppId`, `EntityTypeAttribute<TApp>`, and `EntityTypeId(entityType, appId)`. Built-in partitions are `DefaultApp` (0), `NexusApp` (126), and `TestApp` (127); `[TestEntityType(byte)]` selects the test partition. `SourceKnownEntity.GetAppId` and `GetEntityTypeId` expose entity metadata.

### Bug Fixes

*   **Pagination**: `PaginationRequest.From()` defaults to page size 10, preserves omitted options, and resets pagination when size, effective maximum size, or sort direction changes. Page jumps preserve direction and are bounded to ten pages without integer underflow.
*   **IgnoredLog Null Handling**: `IgnoredLog(this object? obj)` returns `false` when given `null` input instead of throwing a `NullReferenceException`.

## Version 0.9.8

Version alignment release; no package-specific behavior changes.

## Version 0.9.7

### Breaking Changes

*   **CancellationScopeKey Namespace And Diagnostics**: `CancellationScopeKey` moved from `DRN.Framework.Utils.Cancellation` to `DRN.Framework.SharedKernel.Cancellation`. Its public `OwnerType` and `Name` accessors and custom identity-revealing `ToString()` output were removed; default struct formatting does not expose key identity.
    *   *Migration*: Update imports from `using DRN.Framework.Utils.Cancellation;` to `using DRN.Framework.SharedKernel.Cancellation;`. Treat keys as opaque values created through the `For(...)` factories. If diagnostics require a human-readable label, keep a caller-owned label alongside the key rather than parsing or inspecting the key.
*   **Repository Cancellation Default**: When `RepositorySettings<TEntity>.ScopeKey` is `null`, repositories use the root cancellation scope, so `CancelChanges()` and `CancelWhen(token)` affect root-linked operations.
    *   *Migration*: Set `ScopeKey` to a stable `CancellationScopeKey` when a repository group must remain isolated.

### New Features

*   **CancellationScopeKey Primitive**: `CancellationScopeKey` is now available in `DRN.Framework.SharedKernel.Cancellation`. Its `For(...)` factories create type-owned or ownerless named keys, `IsValid` distinguishes factory-created keys from the invalid default value, and names use ordinal equality and may be empty or whitespace but not `null`.
*   **Repository Cancellation Settings**: `RepositorySettings<TEntity>` now includes `ScopeKey` for configuring child cancellation scopes on repository instances.

### Bug Fixes

*   **Pagination Required-Member Construction**: `PaginationResultInfo` now marks its fully initializing JSON constructor as satisfying inherited required members, allowing direct construction without redundant object initializers.

## Version 0.9.6

### Security

*   **Secure TempPath Directory**: `AppConstants.TempPath` now resolves from `DrnAppDataSettings__TempPath`, `DrnAppDataSettings__DataPath/Temp`, then local app data `Temp`, avoiding predictable shared temp roots (CWE-377/CWE-379).

### New Features

*   **Shared Extension Methods**: Moved casing and safe path helpers to `DRN.Framework.SharedKernel.Extensions`.

### Breaking Changes

*   **Repository Cancellation API**: `ISourceKnownRepository<TEntity>.CancellationToken` is now read-only, and `MergeCancellationTokens(token)` was replaced by `CancelWhen(token)`. Remove direct token assignments and use `CancelWhen(token)` for lifetime cancellation links.
*   **AppConstants TempPath Ownership**: `AppConstants.TempPath` resolves only the temp root. Use `IAppData` for directory creation, cleanup, and child paths.

## Version 0.9.5

### Changed

*   **.NET Version Alignment**: Package release aligned with the DRN.Framework 0.9.5 dependency wave for .NET 10.0.9.

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

### Breaking Changes

*   **`ToUnsecure` → `ToPlain`**: Renamed on `ISourceKnownEntityIdOperations`, `SourceKnownEntity`, and `ISourceKnownRepository<TEntity>`. Semantics unchanged — rename call sites to compile.

## Version 0.8.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals, rooted in his timeless words that 'science is the truest guide in life.' In that spirit, and to honor the 14 March Scientists Day, this release is dedicated to the researchers working for the benefit of humanity, and to the rejection of my first academic paper :) ([JOSS #10176](https://github.com/openjournals/joss-reviews/issues/10176)).

## Version 0.7.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and honors 8 March, International Women's Day, a cause inseparable from his vision of equality. This release is dedicated to freedom of speech, democracy, women's rights, and Prof. Dr. Ümit Özdağ, a defender of Mustafa Kemal Atatürk’s enlightenment ideals.

> [!WARNING]
> Since v0.6.0 (released 10 November 2024), substantial changes have occurred. This release notes file has been reset to reflect the current state of the project as of 08 March 2026. Previous history has been archived to maintain a clean source of truth based on the current codebase.

### New Features

*   **Domain Primitives**
    *   **SourceKnownEntity**: Base class implementing `IHasEntityId`, `IEquatable`, and `IComparable`. Features internal `long Id`, external `Guid EntityId`, and optimistic concurrency (`ModifiedAt`).
    *   **AggregateRoot**: Marker base for DDD roots.
    *   **Domain Events**: `DomainEvent` base with specialized `EntityCreated`, `EntityModified`, and `EntityDeleted` variants.
    *   **Identity System**: `[EntityType(byte)]` attribute for type discrimination. `SourceKnownId` structure for high-performance distributed IDs.
*   **Repository & Data Access**
    *   **ISourceKnownRepository**: Standardized contract for `GetAsync`, `GetOrDefaultAsync`, `GetEntityId` (validation), and batch operations.
    *   **Advanced Pagination**: `PaginationRequest` with `PageCursor` (FirstId/LastId) for stable bidirectional navigation (`Next`, `Previous`, `Refresh`).
    *   **Filtering**: `EntityCreatedFilter` for date-range queries.
*   **Exception System**
    *   **Hierarchy**: `DrnException` based types mapping to HTTP status codes.
    *   **Factory Methods**: `ExceptionFor.NotFound`, `Validation`, `Unauthorized`, `Forbidden`, `Conflict`, `Expired`, `UnprocessableEntity`, `Configuration`, `MaliciousRequest`.
    *   **Categorization**: Support for exception `Category` and `Status` properties.
*   **JSON Conventions**
    *   **Web Defaults**: `JsonSerializerDefaults.Web` active by default.
    *   **Enhancements**: `JsonStringEnumConverter`, `CamelCase`, `AllowTrailingCommas`, `NumberHandling.AllowReadingFromString`, and `Int64` string conversion.
*   **Core Utilities & Constants**
    *   **AppConstants**: Global access to `ProcessId`, `AppInstanceId`, `EntryAssemblyName`, `TempPath`, and `LocalIpAddress`.
    *   **Security Attributes**: `[SecureKey]` for string validation and `[IgnoreLog]` to prevent leaking sensitive data in logs.
*   **Entity ID Operations**
    *   **ISourceKnownEntityIdOperations Interface**: Extracted core entity ID operations (`Generate`, `Parse`, `ToSecure`, `ToUnsecure`) into a SharedKernel interface, replacing internal `Func` delegate fields with a single typed contract. `ISourceKnownEntityIdUtils` in Utils inherits this interface.
    *   **Entity Secure Conversion**: `SourceKnownEntity` now exposes `ToSecure` / `ToUnsecure` methods for idempotent conversion between encrypted and plaintext `SourceKnownEntityId` forms.
    *   **Repository Secure Conversion**: `ISourceKnownRepository<TEntity>` now exposes `ToSecure` / `ToUnsecure` methods for converting entity IDs at the repository level.

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/.agent/rules/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
