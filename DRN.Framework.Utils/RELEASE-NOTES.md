Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.9.3

Dependencies upgraded to dotnet 10.0.7

## Version 0.9.2

Dependencies upgraded to dotnet 10.0.6

## Version 0.9.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and is proud to inherit his spiritual legacy: 'I am not leaving behind any definitive text, any dogma, any frozen, rigid rule as my spiritual legacy. My spiritual wealth is science and reason. Those who wish to embrace me after my death will become my spiritual heirs if they accept the guidance of reason and science on this fundamental axis.'

### Breaking Changes

*   **Binary-Incompatible SKEID byte layout** (`SourceKnownEntityIdUtils`): UUID layout migrated to RFC 9562 big-endian; MAC relocated to contiguous bytes 12–15; epoch at byte 0; upper-half MSB sign-toggled for lexicographic sort correctness; lower half split across byte 5 and bytes 9–11.
*   **Timestamp precision: seconds → 250ms ticks** (`EpochTimeUtils`, `TimeStampManager`, `SourceKnownIdUtils`): `ConvertToSeconds` renamed to `ConvertToTicks` (250ms units). `TimeStampManager.UtcNow` truncates to nearest 250ms boundary. Epoch-range guard uses `MaxEpochTicks` (2³³ − 1).
*   **`ToUnsecure` → `ToPlain`** / **`GenerateUnsecure` → `GeneratePlain`** (`SourceKnownEntityIdUtils`).
*   **`NumberBuilder.GetLong` / `NumberParser.Get(long)` residue default**: 31 → 32 bits.
*   **Capacity rebalanced** (`SourceKnownIdUtils`): AppId 6→7 bits (max 127), AppInstanceId 5→6 bits (max 63), Sequence 21→18 bits (262,143/tick). `MaxAllowedDriftSeconds` const: 3s → 5s.

> [!WARNING]
> This is a binary-incompatible change. Entity IDs generated with v0.8.0 will not parse correctly in v0.9.0 — IDs must be regenerated. No migration tooling is provided; there are no expected production consumers with persisted v0.8.x entity IDs.

### New Features

*   **250ms timestamp precision**: New `TimeStampManager` constants: `PrecisionUnitInMs = 250`, `TicksPerPrecisionUnit = 2,500,000`. Epoch-half constants in `SourceKnownIdUtils`: `TicksPerHalf`, `MaxEpochTicks`. Correct sign-bit logic: first half → negative SKID, second half → positive SKID; monotonic ordering preserved.
*   **`NexusAppSettings` constructors**: Added `(byte appId, byte appInstanceId)` overload for programmatic instantiation.
*   **Throughput**: ~1,048,576 IDs/s per generator (262,143 × 4 ticks/s); up to ~8.6B IDs/s with 8,192 generators.

## Version 0.8.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals, rooted in his timeless words that 'science is the truest guide in life.' In that spirit, and to honor the 14 March Scientists Day, this release is dedicated to the researchers working for the benefit of humanity, and to the rejection of my first academic paper :) ([JOSS #10176](https://github.com/openjournals/joss-reviews/issues/10176)).

### Breaking Changes

*   **SKEID Marker Migration (UUID V4 → V8)**: `SourceKnownEntityIdUtils` markers migrated from `0x4D` (UUID V4) to `0x8D` (UUID V8) for RFC 9562 §5.8 compliance.

> [!WARNING]
> This is a binary-incompatible change. Entity IDs generated with v0.7.0 (`0x4D8D` markers) will not parse correctly in v0.8.0. No migration tooling is provided as there are no production consumers with persisted v0.7.0 entity IDs.

### New Features

*   **Clock Drift Detection**: `TimeStampManager` now detects backward clock drift:
    *   Minor drift (<3 seconds): Cached timestamp is frozen (freeze-and-ride-through strategy). `UtcNowTicks` continues serving the previous higher value until the real clock catches up. No blocking or spin-wait.
    *   Critical drift (>=3 seconds): `ClockDriftException` is set and `ApplicationLifetime.RequestShutdown()` is called to initiate graceful shutdown. All subsequent `UtcNowTicks` / `UtcNow` calls throw `ClockDriftException`.
    *   New types: `ClockDriftException`, `ApplicationLifetime`.

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
