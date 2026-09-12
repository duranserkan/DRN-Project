Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.10.0

### Breaking Changes

*   **Entity ID Format Contracts**: GUID operations add `SourceKnownEntityIdFormat` (`ConfiguredDefault = 0`, `Secure = 1`, `Plain = 2`, `Auto = 3`), defaulting to `ConfiguredDefault`. Custom `ISourceKnownEntityIdOperations` implementations must expose an immutable Secure/Plain `DefaultFormat`. Rebuild consumers and update GUID method signatures and method-group bindings. Format is enforced when parsing GUIDs; parsed-record validation takes no format argument and does not reauthenticate GUIDs. Null GUIDs remain supported; undefined formats throw even for null GUIDs and empty GUID batches.
*   **Explicit Expected Identity**: Replace byte-only identity arguments with `new EntityTypeId(entityType, expectedAppId)`: `Generate(long, byte)` becomes `Generate(long, EntityTypeId)`, `HasSameEntityType(byte)` becomes `HasSameEntityTypeId(EntityTypeId)`, and `Validate(byte)` becomes `Validate(EntityTypeId)`. Domain `GetEntityId` accepts this composite identity or a generic entity type. Validation checks both components; `ValidateId()` and the domain GUID helper's boolean option remain validity-only.
*   **Generation Time Policy**: New IDs use a minimum of `2026-09-09T00:00:00Z` and an epoch of `2025-01-01T00:00:00Z` by default. Configure overrides through `SourceKnownGenerationTime.Initialize(minimumUtc, defaultEpoch)` before startup or first ID/epoch use; an explicit epoch requires a minimum. Both values then freeze. Historical reads remain exempt from the floor, and every service and restart using a dataset must retain its origin. See [configuration and limits](README.md#trusted-minimum-generation-time).
*   **Entity Partitions and Analyzers**: Annotate concrete, effectively non-private entities with `[EntityType<TApp>(byte)]`, where `TApp : IAppId`, or a supported derived attribute. Entity type values must be unique within each AppId. New transitive analyzers enforce valid declarations and AppIds, flag duplicate names, and require `<AllowMultipleAppIds>true</AllowMultipleAppIds>` for production projects combining partitions. Derived attributes must forward one byte or byte-backed enum argument unchanged. See [diagnostics and migration requirements](README.md#compile-time-roslyn-analyzers).

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
