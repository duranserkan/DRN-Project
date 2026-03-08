Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.7.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and honors 8 March, International Women's Day, a cause inseparable from his vision of equality. This release is dedicated to freedom of speech, democracy, women's rights, and Prof. Dr. Ümit Özdağ, a defender of Mustafa Kemal Atatürk’s enlightenment ideals.

> [!WARNING]
> Since v0.6.0 (released 10 November 2024), substantial changes have occurred. This release notes file has been reset to reflect the current state of the project as of 08 March 2026. Previous history has been archived to maintain a clean source of truth based on the current codebase.

### New Features

*   **Attribute-Based Dependency Injection**
    *   **Comprehensive Lifetimes**: `[Singleton]`, `[Scoped]`, `[Transient]`, `[HostedService]`, `[Config]`, `[ConfigRoot]`, and Keyed variants (`[SingletonWithKey]`, `[ScopedWithKey]`, `[TransientWithKey]`).
    *   **Registration**: `AddServicesWithAttributes()` auto-scans assemblies. `ValidateServicesAddedByAttributesAsync()` verifies resolution at startup.
    *   **Module Pattern**: `HasServiceCollectionModuleAttribute` for custom registration logic.
    *   **Test Helpers**: `ReplaceInstance`, `ReplaceScoped`, `ReplaceTransient`, `ReplaceSingleton` overrides for integration tests.
*   **Configuration System**
    *   **IAppSettings**: Strong-typed access to config with support for Connection Strings and Sections.
    *   **Environment Helpers**: `IsDevelopmentEnvironment` and `IsStagingEnvironment` properties for explicit environment checks.
    *   **[Config] Attribute**: Bind classes directly to config sections (e.g., `[Config("Payment")]`). Support for `[ConfigRoot]`.
    *   **Layered Sources**: Loads `appsettings`, `appsettings.{Env}`, User Secrets, Env Vars, and **Mounted Settings** (`/appconfig/json-settings/*.json`, `/appconfig/key-per-file-settings`).
    *   **Environment-Aware Auto-Migration**: `DrnDevelopmentSettings.AutoMigrateDevelopment` (default `true`) and `AutoMigrateStaging` (default `false`) replace the previous single `AutoMigrate` flag, enabling per-environment migration control.
*   **Ambient Context & Scoped Cancellation**
    *   **ScopeContext**: Centralized access to `UserId`, `TraceId`, `Authenticated` status, and ambient `IAppSettings`/`IScopedLog`. Built-in RBAC helpers.
    *   **ICancellationUtils**: Scoped cancellation management supporting token merging and lifecycle control.
*   **Scoped Logging & Diagnostics**
    *   **IScopedLog**: Request aggregation of actions, properties, and exceptions. `Measure()` for performance tracking and counting.
    *   **DevelopmentStatus**: Runtime tracking of DB model changes and migration status with environment-aware migration decisions (Development and Staging).
*   **Advanced Data & Bit Packing**
    *   **Bit Packing**: `NumberBuilder` and `NumberParser` (ref structs) for high-performance custom data structures and bit manipulation.
    *   **Monotonic Pagination**: `IPaginationUtils` for temporal cursor-based pagination leveraging entity IDs.
    *   **Cryptographic Helpers**: Unified `HashExtensions` (Blake3, XxHash3), `EncodingExtensions` (Base64, Base64Url, Hex), and `SafeApplyMergePatch` (RFC 7386).
*   **HTTP & Temporal IDs**
    *   **Resilient HTTP**: `IInternalRequest`/`IExternalRequest` with enriched `HttpResponse<T>` diagnostics and Flurl integration.
    *   **Temporal IDs**: `ISourceKnownIdUtils` and `ISourceKnownEntityIdUtils` providing globally sortable identifiers.
    *   **Secure Entity IDs**: AES-256-ECB single-block encrypted `SourceKnownEntityId` variants with flag-based dispatch via `UseSecureSourceKnownIds` (defaults to `true`).
        *   `GenerateSecure` / `GenerateUnsecure` explicit methods; `Parse` auto-detects encrypted and plaintext IDs.
        *   Post-quantum ready — AES-256 retains 128-bit security under Grover's algorithm.
    *   **Epoch-Based Time Addressing**: `SourceKnownEntityId` byte 5 reserved for epoch indexing, enabling ~34,842 monotonic time years starting from 2025-01-01. Each epoch spans ~136 years (2³¹ seconds × 2 epoch halves). The first epoch requires no configuration.
    *   **ISourceKnownEntityIdOperations Inheritance**: `ISourceKnownEntityIdUtils` now inherits `ISourceKnownEntityIdOperations` (SharedKernel), formalizing the core contract (`Generate`, `Parse`, `ToSecure`, `ToUnsecure`) for cross-layer use without Utils dependency.
    *   **Secure ↔ Unsecure Conversion**: `ToSecure` / `ToUnsecure` methods (with nullable overloads) on `SourceKnownEntityIdUtils` for idempotent conversion between encrypted and plaintext `SourceKnownEntityId` forms.
    *   **Named Constants for GUID Layout**: Replaced magic numbers in `SourceKnownEntityIdUtils` with named constants (`GuidLength`, `MacHashLength`, `MacHashFirstIndex`–`MacHashFourthIndex`) for improved readability and maintainability.
*   **Concurrency**
    *   **Lock-Free Atomics**: `LockUtils` static helpers (`TryClaimLock`, `TryClaimScope`, `ReleaseLock`, `TrySetIfEqual`, `TrySetIfNull`, `TrySetIfNotEqual`, `TrySetIfNotNull`) for lock-free coordination using `Interlocked`. Includes disposable `LockScope` ref struct for automatic lock release via `using`.
*   **Core Extensions & Time**
    *   **Reflection**: Optimized `MethodUtils` with caching, `CreateSubTypes`, and deep discovery (`GetGroupedPropertiesOfSubtype`).
    *   **Extensions**: Robust set of `string` (Casing, Parsing), `FileInfo` (Efficient line reading), `Stream` (Size guards), and `Dictionary` utilities.
    *   **High-Perf Time**: `TimeStampManager` (cached UTC seconds) and `RecurringAction` (async-safe timers).

---

Documented with the assistance of [DiSCOS](https://github.com/duranserkan/DRN-Project/blob/develop/DiSCOS/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
