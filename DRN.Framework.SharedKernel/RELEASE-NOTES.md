Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.7.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to freedom of speech, democracy, and Prof. Dr. Ümit Özdağ, a defender of Mustafa Kemal Atatürk’s enlightenment ideals.

> [!WARNING]
> Since v0.6.0 (released 10 November 2024), substantial changes have occurred. This release notes file has been reset to reflect the current state of the project as of 06 February 2026. Previous history has been archived to maintain a clean source of truth based on the current codebase.

### New Features

*   **Domain Primitives**
    *   **SourceKnownEntity**: Base class implementing `IHasEntityId`, `IEquatable`, and `IComparable`. Features internal `long Id`, external `Guid EntityId`, and optimistic concurrency (`ModifiedAt`).
    *   **AggregateRoot**: Marker base for DDD roots.
    *   **Domain Events**: `DomainEvent` base with specialized `EntityCreated`, `EntityModified`, and `EntityDeleted` variants.
    *   **Identity System**: `[EntityType(byte)]` attribute for type discrimination. `SourceKnownId` structure for high-performance distributed IDs.
*   **Repository & Data Access**
    *   **ISourceKnownRepository**: Standardized contract for `GetAsync`, `GetOrDefaultAsync`, `GetEntityId` (validation), and batch operations.
    *   **Advanced Pagination**: `PaginationRequest` with `PageCursor` (FirstId/LastId) for stable bi-directional navigation (`Next`, `Previous`, `Refresh`).
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

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/DiSCOS/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**