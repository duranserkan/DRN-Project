Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.9.9

### New Features

*   **AesGcmEncryptorBase Cryptographic Primitive**: Added `AesGcmEncryptorBase` and `AesGcmEncryptedData` under `DRN.Framework.Utils.Data.Encryption` for clean, context-separated AES-256-GCM symmetric encryption and decryption with automatic BLAKE3 subkey derivation from `IAppSecuritySettings`.
*   **Context-Aware AesGcm Key Derivation**: Added `CreateAesGcm(string context)` on `IAppSecuritySettings` / `AppSecuritySettings` to derive a dedicated 32-byte key from `AppEncryptionKey` via BLAKE3 with automatic intermediate memory zeroing and return an initialized `AesGcm` cipher.
*   **SeedKey Security Enforcements**: `AppSettings` now rejects the default `DrnAppFeatures.DefaultSeedKey` when `Environment` is not `Development` (`Staging`, `Production`, `NotDefined`), and rejects the well-known `DrnAppFeatures.SampleSeedKey` whenever `TestEnvironment.DrnTestContextEnabled` is not set, throwing a `ConfigurationException` at startup to prevent insecure key material in live deployments.
*   **Sample NexusKey Material Forbidden**: `NexusKey` now unconditionally forbids the sample key material (`sample-nexus-key-material-000000`) across all environments and test executions, throwing a `ConfigurationException` at construction time.
*   **HttpMessageHandler DI Injection**: `InternalRequest` and `ExternalRequest` now accept an optional `HttpMessageHandler?` via dependency injection, allowing in-memory routing handlers (such as `ApplicationContextRouterHandler` during integration testing) to intercept and route Flurl calls without global static cache mutation.
*   **Entity-Aware AppId Derivation**: `SourceKnownIdUtils.Next<TEntity>()` now automatically derives `AppId` from `SourceKnownEntity.GetAppId` when `TEntity` derives from `SourceKnownEntity`, ensuring generated 64-bit IDs always match the domain entity's declared partition while falling back to `NexusAppSettings.AppId` for non-entity types.
*   **Composite EntityTypeId Validation & Generation**: Added `Validate(Guid, EntityTypeId)` and `Generate(long, EntityTypeId)` overloads on `ISourceKnownEntityIdUtils` and `SourceKnownEntityIdUtils` with non-breaking default interface method implementations. Generic `Generate<TEntity>(long)` and `Generate(SourceKnownEntity)` now enforce composite `(EntityType, AppId)` partition validation at generation time to prevent cross-partition ID construction.
*   **DrnServiceContainer Backward Compatibility**: Preserved the original 2-parameter public constructor `DrnServiceContainer(Assembly, LifetimeAttribute[])` alongside the 3-parameter overload to maintain binary compatibility and prevent `MissingMethodException` for precompiled consumers.

## Version 0.9.8

Dependencies upgraded to dotnet 10.0.11

### New Features

*   **NexusAppSettings MacType Configuration**: Added `NexusMacType` enum (`Blake3 = 1`) and `MacType` property on `NexusAppSettings` (defaulting to `NexusMacType.Blake3`) with configuration validation to explicitly specify MAC hashing algorithms.

## Version 0.9.7

### Breaking Changes

*   **CancellationScopeKey Relocation**: Moved `CancellationScopeKey` to `DRN.Framework.SharedKernel.Cancellation`. Callers should update namespace imports from `DRN.Framework.Utils.Cancellation` to `DRN.Framework.SharedKernel.Cancellation`.
*   **JSON Merge Patch API Split And Result Type**: Removed the `changeOriginal` Boolean from `JsonMergePatch.SafeApplyMergePatch`; the method now always leaves both inputs unchanged and reuses the target only for semantic no-ops. Use the new `ApplyMergePatchInPlace` method when in-place mutation is required. `MergeResult` changed from a record class to a readonly record struct, and its `Json` value is nullable because `System.Text.Json` represents root-level JSON `null` as a null `JsonNode`.
*   **Opaque Cancellation Scope Keys**: Removed the public `CancellationScopeKey.OwnerType` and `Name` identity accessors and the custom identity-revealing `ToString()` output. Continue creating type-owned keys with `For<T>()`, `For(Type)`, `For<T>(name)`, or `For(Type, name)` instead of inspecting their identity.
*   **HTTP Response Snapshots**: Removed `HttpResponse.Response` and changed public wrapper construction to accept an HTTP status. Use `HttpStatus` and `Payload`; dispose streaming wrappers after use.
*   **HTTP Response Conversion API**: Removed static conversion methods from `HttpResponse`. Call `ToStringAsync`, `ToBytesAsync`, `ToStreamAsync`, `FromJsonAsync<T>`, and their non-throwing `Try*` counterparts as extension methods on `Task<IFlurlResponse>` or `IFlurlResponse`.
*   **HTTP Response Ownership**: `HttpResponse<TResult>` is now sealed and implements `IDisposable`. Disposing it also disposes an `IDisposable` payload.
*   **Scope Data Authentication Separation**: `ScopeData` no longer stores role checks or parses claim strings. Its `Roles`, `IsRoleExists`, `SetParameterAsRole`, `SetParameterAsFlag`, and string-parsing `SetParameter` members were removed. Use `IScopedUser.IsInRole` / `ScopeContext.IsUserInRole` for roles, `IScopedUser.GetClaimParameter<TValue>` / `ScopeContext.GetClaimParameter<TValue>` for claims, and the typed `ScopeData.SetFlag` / `SetParameter` methods for caller-owned ambient values.

### New Features

*   **In-Place JSON Merge Patching**: Added `JsonMergePatch.ApplyMergePatchInPlace` overloads (`ref JsonNode? target` and `JsonObject target`) for executing full RFC 7396 merge patches in-place while preserving nested object references and updating root node references when necessary.
*   **Ownerless Named Cancellation Keys**: Support creating named cancellation keys without an owner type via `CancellationScopeKey.For(name)` for intentional cross-type groups. Ownerless keys share one ordinal-name namespace within the current cancellation service scope, so qualified, centrally defined names are recommended. Empty and whitespace names are valid, but the key name must not be null.
*   **AES-256 Single-Block Implementations**: Added `Aes256`, a disposable `Vector128<byte>` AES-256 ECB primitive with explicit x86/ARM runtime-intrinsic methods, explicit portable .NET AES methods, and default methods that use runtime intrinsics with automatic portable fallback. One live instance supports concurrent operations and clears or disposes its key state. Source-known ID encryption reuses the fallback methods without changing the encrypted ID format or requiring AES-intrinsic hardware.

### Changed

*   **JSON Merge Patch Allocation Model**: Safe changed-object merges clone the target once and mutate that detached tree without repeated nested subtree clones; semantic no-ops return the original target without a result allocation. The object-only in-place API avoids target-tree cloning for disjoint inputs and preserves existing nested object references.
*   **JSON Merge Patch Standard Reference**: Current APIs and documentation target RFC 7396, which obsoletes RFC 7386; historical release-note blocks retain their originally published wording.
*   **Configuration Debug View Security Guidance**: Package documentation now clarifies that `ConfigurationDebugView` summary redaction is best-effort, key-name-based redaction rather than a security boundary; protect debug summaries as potentially sensitive data.
*   **Inspectable HTTP Call Results**: Added HTTP status-class inspection and non-throwing `TryToStringAsync`, `TryToBytesAsync`, `TryToStreamAsync`, and `TryFromJsonAsync<T>` converters. Try results distinguish redirects, client errors, server errors, transport/timeouts, response-read failures, and JSON deserialization failures while preserving cancellation and response ownership semantics. `ThrowIfFailure()` rethrows captured processing failures with their original stack after inspection without treating HTTP error statuses as exceptions. Raw diagnostic exception messages and exception objects are excluded from System.Text.Json serialization.
*   **JIT-Safe Assembly Scanning**: Split `AddServicesWithAttributes` into a parameterless convenience overload (protected against JIT compiler inlining with `[MethodImpl(MethodImplOptions.NoInlining)]`) and an explicit `Assembly` overload. This prevents tail-call or inlining optimizations from unexpectedly altering assembly scanning results.
*   **Claim Lookup Allocation**: `ClaimGroup.GetValue`, `ClaimExists`, and `FindClaim` now traverse the frozen claim set directly instead of constructing LINQ iterators for single-result lookups.
*   **Scoped-User Typed Claims**: `IScopedUser` now exposes typed claim parsing through `GetClaimParameter<TValue>`, and `ScopeContext` delegates to that contract. `ScopedUser` resolves and parses each existing claim on demand without a separate parsed-value cache; missing claims return the supplied typed default unchanged so issuer, target-type, and default choices stay call-local.
*   **Scoped Cancellation Guidance**: Package guidance now distinguishes type-owned shared groups from caller-owned operation cancellation and documents type-only keys with optional names.

### Bug Fixes

*   **NumberParser Reset Type Safety**: `ResetToParse` now rejects values whose signedness does not match the parser instead of resetting the cursor while silently retaining the previous parsed value.
*   **Object Configuration Stream Reusability**: `ObjectToJsonConfigurationProvider.Load()` now serializes its source object into a fresh stream on each configuration build, allowing reusable configuration sources (such as test context dynamic options) to be built multiple times without throwing stream-disposed `ArgumentException` errors.
*   **Bounded Stream Materialization Defaults**: `StreamExtensions.ToArrayAsync`/`ToBinaryDataAsync` and `JpegValidator.Validate`/`ValidateAsync` methods now default to 10MB bounds (`DefaultMaxStreamSize` / `DefaultMaxJpegSize`) rather than `long.MaxValue`, preventing unconstrained stream materialization into memory when callers omit explicit bounds.
*   **Dependency Injection Provider Isolation**: Attribute registration now caches only immutable assembly scan metadata. Each service collection owns its `DrnServiceContainer` and module state, and startup validation no longer shares resolved service-type state across providers.
*   **LockUtils Reference Identity**: `TrySetIfNotEqual` now uses reference identity consistently with its `Interlocked.CompareExchange` CAS operation, so distinct value-equal records and classes no longer block an otherwise valid update.
*   **Entity Date Filter Tick Boundaries**: Date filters now apply inclusive and exclusive boundaries to the full 250ms Source-Known ID tick, preventing nonzero app, instance, and sequence payloads from being incorrectly included or excluded at range edges.
*   **Ambient Claim Isolation**: `ScopeContext` claim helpers now resolve every typed lookup independently by claim type, issuer, and requested target type. Issuer, type, and default-value choices can no longer contaminate later reads that use the same claim name.
*   **Multi-Identity Authentication**: `ScopedUser.Authenticated` now matches ASP.NET Core authorization by accepting any authenticated identity, treats an empty principal as anonymous, selects an authenticated primary identity, and excludes unauthenticated identities from ambient claims.
*   **Recurring Action Stop Semantics**: `RecurringAction.Stop()` now prevents an active callback from rescheduling the timer after the callback completes. A later `Start()` resumes scheduling normally.
*   **Object Reflection Depth Enforcement**: Fixed `GetGroupedPropertiesOfSubtype` recursion depth limit check to correctly increment depth levels along nested property traversal chains. Invalid (non-positive) recursion limits now trigger an `ArgumentOutOfRangeException`.
*   **JSON Merge Patch RFC, Change Tracking, And Depth Enforcement**: Object patches now merge recursively against empty objects when targets are missing or non-object, omitting null members per RFC 7396. Root-level JSON null is supported, no-op deletions and equivalent replacements no longer report `Changed`, and the complete patch depth is validated before applying changes. Unit coverage includes all 15 RFC 7396 Appendix A examples.
*   **Width-Aware Signed NumberBuilder Initialization**: Signed integer builders now initialize and reset the sign bit at the selected numeric width, preserving negative defaults when building 32-bit values.
*   **HTTP Response Disposal**: Converters now dispose responses when payload reading fails. Streaming conversion also disposes the response if stream retrieval fails.
*   **Claim Parameter Fallback**: `IScopedUser.GetClaimParameter` and `ScopedUser.GetClaimParameter` now return the caller-provided `defaultValue` fallback when claim value parsing fails.
*   **Scope Data Stored Null Handling**: `ScopeData.GetParameter<TValue>` now returns `null` for explicitly stored null entries when `TValue` is a nullable type, returning `defaultValue` only for missing keys, incompatible stored types, or non-nullable target types.

## Version 0.9.6

### Breaking Changes

*   **AppSecuritySettings BLAKE3 Derivation**: `AppHashKey`, `AppEncryptionKey`, `AppKey`, and `AppSeed` are now derived from `DrnAppFeatures.SeedKey` through BLAKE3 derive-key mode with distinct DRN Framework context strings. This replaces the previous custom SHA/BLAKE3 hash chains and changes app-specific names, rate-limit redaction hashes, Development default Nexus key material, and seed-dependent operations.
*   **NexusKey BLAKE3 Derivation**: `NexusKey` now derives both `MacKey` and `EncryptionKey` from decoded 32-byte key material through BLAKE3 derive-key mode with distinct DRN Framework context strings. This replaces the previous custom hash-chain derivation and changes generated secure IDs; existing IDs may require migration, regeneration, or an explicit compatibility strategy.
*   **Development Nexus Key Material Derivation**: When `Development` has no explicit default Nexus key, `AppSettings` now derives deterministic 32-byte Base64Url key material with BLAKE3 derive-key mode from `AppSecuritySettings` context-derived values instead of the previous custom hash-chain. Development-generated secure IDs may require migration, regeneration, or an explicit compatibility key.
*   **Legacy Nexus Key Configuration**: `AppSettings` now rejects legacy `NexusAppSettings:MacKeys` configuration before Development key auto-generation, preventing old key material from being silently ignored. Migrate `MacKeys[*].Key` to `Keys[*].KeyMaterial` and move matching `Format` and `Default` values to `Keys[*].Format` and `Keys[*].Default`.
*   **Casing And Path Extension Relocation**: Casing and safe path helpers moved from Utils to `DRN.Framework.SharedKernel.Extensions`.
*   **Settings Classes Sealed**: `AppSettings`, `NexusAppSettings`, and `NexusKey` are now sealed.
*   **Explicit Cancellation Root**: Removed the bare `ICancellationUtils.Token`, `IsCancellationRequested`, `Merge`, and `Cancel` members. Migrate root-wide calls to `cancellation.Root.Token`, `cancellation.Root.IsCancellationRequested`, `cancellation.Root.Merge(token)`, and `cancellation.Root.Cancel()`. Use `GetOrCreateScope(key)` for component or workflow groups and a caller-owned linked token source for instance-specific or operation-specific isolation.

### New Features

*   **App Data Roots**: Added `IAppData` and related types for validated temp/data roots, startup temp cleanup, and safe child paths.
*   **Cancellation Scopes**: Added typed child scopes through `CancellationScopeKey`. The same key resolves to the same scope and token, while root cancellation reaches every existing and later-created child.

### Bug Fixes

*   **AppSettings Nexus Key Validation**: Configured `NexusAppSettings:Keys` entries are now validated before default-key inspection, so null key entries report the intended configuration error instead of a null-reference failure.
*   **Settings Disposal Idempotency**: Settings now ignore repeated disposal and dispose owned key material once.
*   **Scoped Cancellation Composition**: Root and child scopes keep the same token for their lifetime, repeated merges do not duplicate registrations, and existing consumers observe later external or manual cancellation.
*   **Cancellation Lifecycle**: Reentrant or repeated cancellation and disposal are safe, caller-owned token sources remain untouched, and merged-token resources are released promptly.

## Version 0.9.5

### New Features

*   **Rate Limiting Settings**: Added validated `DrnAppFeatures:DrnRateLimit` knobs for DRN Hosting rate limiting (`Disabled`, partition log mode, shared token limit, replenishment period, tokens per period, B2B-friendly pre-auth defaults, and optional pre-auth/post-auth overrides), exposed in code as `IAppSettings.Features.RateLimit`.
*   **Test Scope Initialization**: `ScopeContext.InitializeForTest(...)` is now public and resets the current async-local scope before initialization, preventing stale test scope data from leaking between helper calls.
*   **Stream Hashing Support**: Added stream and file hashing overloads in `HashExtensions` (supporting Blake3, XxHash3, Sha256, Sha512, and keyed Blake3/XxHash3 algorithms) to hash files and large payloads without first materializing them as `BinaryData`.
*   **JPEG Payload Validation**: Added public `DRN.Framework.Utils.Validators.JpegValidator` with explicit `JpegValidationResult` and `JpegValidationErrorReason` support for structural, stream-based, and size-bounded JPEG byte validation before persisting uploaded image payloads.
*   **Path Security Extensions**: Added `PathExtensions.NormalizeDirectoryPath` (full-path resolution with trailing-separator cleanup) and `IsPathWithinDirectory` (segment-aware containment check using OS-correct path comparison) for safe path validation in file-serving and manifest processing.
*   **ScopedLog.CopyFrom**: New method on `IScopedLog` for merging log data, exception, and warning state from one scoped log into another with defensive value cloning for mutable collection types.
*   **Configuration Debug View Redaction**: `ConfigurationDebugView` now redacts sensitive configuration values (connection strings, passwords, secrets, tokens, API keys, credentials) by default. A new `GetDebugView(bool includeRawValues)` overload on `IAppSettings` allows opt-in to raw values, but raw inclusion is only permitted in the Development environment.
*   **Strict Nexus MAC Key Formats**: `NexusMacKey` now records a `ByteEncoding` `Format` (`Utf8`, `Hex`, `Base64`, or `Base64UrlEncoded`) and accepts only values that resolve directly to exactly 32 bytes. Development auto-generation remains deterministic from `SeedKey` and uses the framework `Hash()` default Base64Url output.
*   **SourceKnownEntityId Key-Ring Fallback**: `SourceKnownEntityIdUtils` generates with the default Nexus MAC key and parses with a default-first key ring so IDs generated before key rotation remain parseable while old keys stay configured.

### Bug Fixes

*   **Configuration Debug View**: Continues traversing child configuration keys when a higher-priority provider defines a scalar value for the parent section, and renders entries with the value provider's key casing so CI/environment overrides do not hide or rename lower-provider child entries in debug summaries.
*   **Prototype Recreation Gate**: `DevelopmentStatus` now enables prototype database recreation only in Development, and honors applied migrations: empty databases can still be recreated for prototyping, while databases with applied migrations require `UsePrototypeModeWhenMigrationExists`.

## Version 0.9.4

Dependencies upgraded to dotnet 10.0.8

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
    *   **HTTP Request Wrappers**: `IInternalRequest`/`IExternalRequest` with standardized Flurl integration, HTTP version policy configuration, and enriched `HttpResponse<T>` diagnostics. Retries/circuit breakers are not configured by this package.
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

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/.agent/rules/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
