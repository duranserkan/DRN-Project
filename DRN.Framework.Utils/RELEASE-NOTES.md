Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.7.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to freedom of speech, democracy, and Prof. Dr. Ümit Özdağ, a defender of Mustafa Kemal Atatürk’s enlightenment ideals.

> [!WARNING]
> Since v0.6.0 (released 10 November 2024), substantial changes have occurred. This release notes file has been reset to reflect the current state of the project as of 06 February 2026. Previous history has been archived to maintain a clean source of truth based on the current codebase.

### New Features

*   **Attribute-Based Dependency Injection**
    *   **Comprehensive Lifetimes**: `[Singleton]`, `[Scoped]`, `[Transient]`, `[HostedService]`, `[Config]`, `[ConfigRoot]`, and Keyed variants (`[SingletonWithKey]`, `[ScopedWithKey]`, `[TransientWithKey]`).
    *   **Registration**: `AddServicesWithAttributes()` auto-scans assemblies. `ValidateServicesAddedByAttributesAsync()` verifies resolution at startup.
    *   **Module Pattern**: `HasServiceCollectionModuleAttribute` for custom registration logic.
    *   **Test Helpers**: `ReplaceInstance`, `ReplaceScoped`, `ReplaceTransient`, `ReplaceSingleton` overrides for integration tests.
*   **Configuration System**
    *   **IAppSettings**: Strong-typed access to config with support for Connection Strings and Sections.
    *   **[Config] Attribute**: Bind classes directly to config sections (e.g., `[Config("Payment")]`). Support for `[ConfigRoot]`.
    *   **Layered Sources**: Loads `appsettings`, `appsettings.{Env}`, User Secrets, Env Vars, and **Mounted Settings** (`/appconfig/json-settings/*.json`, `/appconfig/key-per-file-settings`).
*   **Ambient Context & Scoped Cancellation**
    *   **ScopeContext**: Centralized access to `UserId`, `TraceId`, `Authenticated` status, and ambient `IAppSettings`/`IScopedLog`. Built-in RBAC helpers.
    *   **ICancellationUtils**: Scoped cancellation management supporting token merging and lifecycle control.
*   **Scoped Logging & Diagnostics**
    *   **IScopedLog**: Request aggregation of actions, properties, and exceptions. `Measure()` for performance tracking and counting.
    *   **DevelopmentStatus**: Runtime tracking of DB model changes and migration status (Dev only).
*   **Advanced Data & Bit Packing**
    *   **Bit Packing**: `NumberBuilder` and `NumberParser` (ref structs) for high-performance custom data structures and bit manipulation.
    *   **Monotonic Pagination**: `IPaginationUtils` for temporal cursor-based pagination leveraging entity IDs.
    *   **Cryptographic Helpers**: Unified `HashExtensions` (Blake3, XxHash3), `EncodingExtensions` (Base64, Base64Url, Hex), and `SafeApplyMergePatch` (RFC 7386).
*   **HTTP & Temporal IDs**
    *   **Resilient HTTP**: `IInternalRequest`/`IExternalRequest` with enriched `HttpResponse<T>` diagnostics and Flurl integration.
    *   **Temporal IDs**: `ISourceKnownIdUtils` and `ISourceKnownEntityIdUtils` providing globally sortable identifiers.
*   **Concurrency**
    *   **Lock-Free Atomics**: `LockUtils` static helpers (`TryClaimLock`, `TryClaimScope`, `ReleaseLock`, `TrySetIfEqual`, `TrySetIfNull`, `TrySetIfNotEqual`, `TrySetIfNotNull`) for lock-free coordination using `Interlocked`. Includes disposable `LockScope` ref struct for automatic lock release via `using`.
*   **Core Extensions & Time**
    *   **Reflection**: Optimized `MethodUtils` with caching, `CreateSubTypes`, and deep discovery (`GetGroupedPropertiesOfSubtype`).
    *   **Extensions**: Robust set of `string` (Casing, Parsing), `FileInfo` (Efficient line reading), `Stream` (Size guards), and `Dictionary` utilities.
    *   **High-Perf Time**: `TimeStampManager` (cached UTC seconds) and `RecurringAction` (async-safe timers).

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/DiSCOS/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
