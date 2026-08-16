Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.9.9

### Breaking Changes

*   **Compile-Time Roslyn Analyzers**: Added `DRN.Framework.SharedKernel.Analyzers` with error-level diagnostics delivered transitively to all referencing projects and NuGet consumers. Builds will fail for existing codebases if entities violate annotation, uniqueness, or inheritance constraints:
    *   `DRN0001` (*Error*): Enforces that all non-abstract classes descending from `SourceKnownEntity` declare `[EntityType<TApp>(byte)]` (where `TApp : IAppId`) or a domain-derived attribute.
        *   *Migration*: Annotate every concrete (non-abstract) class inheriting from `SourceKnownEntity` with `[EntityType<TApp>(value)]` (using `IAppId` such as `DefaultApp`) or a domain-derived attribute (e.g. `[NexusEntityType(value)]`).
    *   `DRN0002` (*Error*): Enforces that `EntityType` byte values are unique across all entities within the local compilation and referenced assemblies for the same `AppId`. In aggregator projects (such as host apps and integration test suites), reports cross-referenced assembly byte collisions at compilation end while deduplicating shared diamond dependencies.
        *   *Migration*: Ensure every concrete `SourceKnownEntity` subclass within the domain dependency graph is assigned a distinct `EntityType` byte value per `AppId`.
    *   `DRN0003` (*Error*): Prohibits applying `[EntityType]` to abstract classes, private classes, or non-`SourceKnownEntity` types.
        *   *Migration*: Remove `[EntityType]` attributes from abstract base classes (apply only to concrete descendants), private classes, and classes that do not derive from `SourceKnownEntity`.
    *   `DRN0004` (*Warning*): Detects duplicate entity class names across the domain model within the same `AppId` (including cross-referenced assemblies) to guard against EF Core table mapping conflicts and event serialization ambiguity.

### New Features

*   **Application Partitioning (`IAppId`)**: Added strongly-typed application partition metadata via `IAppId` (`AppId` 0..127) and `EntityTypeAttribute<TApp>` to namespace entity type discrimination values and class names across domain modules.
    *   Exposes `IAppId.DefaultAppId` (0), `IAppId.NexusAppId` (126), `IAppId.TestAppId` (127), and `IAppId.MaxAppId` (127) partition constants.
    *   `DefaultApp` (`AppId = 0`, `Value = 0`): Built-in default application partition for standalone domains and single-application systems (`[EntityType<DefaultApp>(byte)]`).
    *   `NexusApp` (`AppId = 126`, `Value = 126`): Built-in Nexus service partition (`[EntityType<NexusApp>(byte)]` or domain-derived `[NexusEntityType(NexusEntityTypes)]`).
    *   `TestApp` (`AppId = 127`, `Value = 127`): Built-in test application partition isolating test entities from production domain entity types.
    *   `[TestEntityType(byte)]`: Convenience attribute (`TestEntityTypeAttribute`) binding test entities directly to `TestApp` (`AppId = 127`) without generic type boilerplate.
    *   `EntityTypeId`: Immutable 2-byte composite identifier (`(EntityType, AppId)`) record struct with `IComparable<EntityTypeId>` support for partition-scoped entity type mappings and validation.

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
