Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

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

Documented with the assistance of [DiSCOS](https://github.com/duranserkan/DRN-Project/blob/develop/DiSCOS/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
