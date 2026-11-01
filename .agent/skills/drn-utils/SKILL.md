---
name: drn-utils
description: "DRN.Framework.Utils - Attribute-based dependency injection, settings, logging, scoped cancellation, ID generation, Base32/TOTP encoding and authentication utilities, entity date filtering, validators, and core utilities. Keywords: dependency-injection, configuration, appsettings, appdata, logging, cancellation, source-known-id, base32, totp, mfa, entity-date-filter, tick-boundary, validators, extensions, http-client"
last-updated: 2026-11-01
difficulty: intermediate
tokens: ~2.7K
---

# DRN.Framework.Utils

> Core utilities: attribute-based DI, configuration, logging, and extensions.

Resolve upstream links and local source through [Documentation And Source References](../overview-drn-framework/SKILL.md#documentation-and-source-references).

## When to Apply
- Setting up dependency injection with attributes
- Accessing configuration via IAppSettings
- Using or extending logging (IScopedLog)
- Coordinating explicit root or keyed scoped cancellation with optional type ownership
- Working with DRN extension methods
- Understanding service registration patterns

---

## Module Registration

```csharp
services.AddDrnUtils(); // Registers attribute-based services, HybridCache, TimeProvider
```

`AddDrnUtils()` registers Microsoft's `HybridCache` with default in-memory caching. Register `IDistributedCache` (e.g., Redis) **before** calling `AddDrnUtils()` to enable distributed mode.

---

## Attribute-Based Dependency Injection

### Lifetime Attributes

| Attribute | Lifetime | Notes |
|-----------|----------|-------|
| `[Singleton<TService>]` | Singleton | `TryAdd` by default |
| `[Scoped<TService>]` | Scoped | Most common |
| `[Transient<TService>]` | Transient | New per resolution |
| `[SingletonWithKey<TService>(key)]` | Keyed singleton | Keyed services |
| `[ScopedWithKey<TService>(key)]` | Keyed scoped | Keyed services |
| `[TransientWithKey<TService>(key)]` | Keyed transient | Keyed services |
| `[HostedService]` | Singleton | Auto-registers `IHostedService` implementations |
| `[Config("Section")]` | Singleton | Binds configuration section to class |
| `[ConfigRoot]` | Singleton | Binds to configuration root |

> [!NOTE]
> All lifetime attributes accept optional `tryAdd` parameter (default: `true`). When `true`, `TryAdd` is used so existing registrations are not overwritten. Set to `false` to allow multiple implementations of the same service type.

```csharp
public interface IMyService { }

[Scoped<IMyService>]
public class MyService : IMyService { }
```

### Registration & Validation

```csharp
sc.AddServicesWithAttributes();                      // Scans calling assembly
serviceProvider.ValidateServicesAddedByAttributesAsync(); // Health check
```

Assembly scan metadata may be cached globally, but `DrnServiceContainer` module state belongs to one service collection and validation deduplication belongs to one provider-validation invocation. Re-registering the same assembly in one collection remains idempotent.

### Module Registration & Startup Actions

Attributes inheriting from `ServiceRegistrationAttribute` handle complex registration and post-startup actions. Example: `DrnContext<T>` uses `[DrnContextServiceRegistration]` to auto-register the DbContext and trigger migrations in Development.

---

## Configuration (IAppSettings)

`DrnAppFeatures:DrnRateLimit` configures Hosting's token buckets. Defaults provide 250 post-auth tokens and 2,500 pre-auth tokens, with matching refills every 60 seconds. Phase override values of 0 inherit shared settings.

```csharp
public interface IAppSettings
{
    IConfiguration Configuration { get; }
    DrnAppFeatures Features { get; }
    DrnLocalizationSettings Localization { get; }
    DrnDevelopmentSettings DevelopmentSettings { get; }
    NexusAppSettings NexusAppSettings { get; }
    AppEnvironment Environment { get; }
    bool IsDevelopmentEnvironment { get; }
    bool IsStagingEnvironment { get; }
    string AppKey { get; }
    string ApplicationName { get; }
    string ApplicationNameNormalized { get; }
    string GetAppSpecificName(string name, string prefix = "_");
    
    bool TryGetConnectionString(string name, out string connectionString);
    string GetRequiredConnectionString(string name);
    bool TryGetSection(string key, out IConfigurationSection section);
    IConfigurationSection GetRequiredSection(string key);
    T? GetValue<T>(string key);
    T? GetValue<T>(string key, T defaultValue);
    T? Get<T>(string key, bool errorOnUnknownConfiguration = false, bool bindNonPublicProperties = true);
    ConfigurationDebugView GetDebugView();
    ConfigurationDebugView GetDebugView(bool includeRawValues);
}
```

`ConfigurationDebugView` redacts secret-looking values by default, lists child keys even when a provider also defines a scalar value for the parent section, and renders summary paths using the value provider's key casing. Object-to-JSON configuration uses the framework JSON defaults, including camelCase keys; explicit key/value configuration preserves the caller-provided key text.

### Config Attribute

```csharp
[Config("MySection")]      // Binds appsettings:MySection
public class MySettings { }

[Config]                   // Uses class name as section
public class FeatureFlags { }

[ConfigRoot]               // Binds to root
public class RootSettings { }
```

### Configuration Sources (in order, later overrides earlier)

Canonical copy: [Maintenance Reference: Configuration Sources](../overview-drn-framework/SKILL.md#maintenance-reference-configuration-sources).

1. `appsettings.json` → 2. `appsettings.{Environment}.json` → 3. User secrets when the application assembly is available → 4. Environment variables → 5. Mounted settings (`/appconfig`) → 6. Command line arguments

`Environment` is required and must be `Development`, `Staging`, or `Production`. DRN validates this value before loading `appsettings.{Environment}.json`; missing, `NotDefined`, or unknown values fail startup with `ConfigurationException`.

Override mount directory via `IMountedSettingsConventionsOverride`.

| Symptom | Solution |
|---------|----------|
| `ConfigurationException` | Add missing key to `appsettings.json` |
| `Environment setting is missing` | Set `Environment` to `Development`, `Staging`, or `Production` |
| Env vars not binding | Use `__` for nested: `Section__Key` |
| Mounted settings not loading | Check `/appconfig/` or override via `IMountedSettingsConventionsOverride` |

When grouping options into nested objects, explicitly validate child objects before relying on child data annotations for startup safety; plain `Validator.TryValidateObject` does not recursively walk nested option objects.

`DrnAppFeatures.SeedKey` feeds `AppSecuritySettings`, which derives `AppHashKey`, `AppEncryptionKey`, `AppKey`, and `AppSeed` through BLAKE3 derive-key mode with distinct DRN Framework context strings. The hash and encryption keys stay Base64Url-encoded 32-byte values, `AppKey` stays an 8-character public discriminator, and `AppSeed` stays a signed 64-bit seed value.

### App Data Roots

`IAppData` exposes `Temp` and `Data`. Override roots before DRN config with `DrnAppDataSettings__TempPath` and `DrnAppDataSettings__DataPath`. `TempPath` appends `EntryAssemblyNameNormalized`: `<TempPath>/<EntryAssemblyNameNormalized>`, then `<DataPath>/Temp/<EntryAssemblyNameNormalized>`, then `<LocalApplicationData>/Temp/<EntryAssemblyNameNormalized>`. A configured data path is used as-is for `LocalAppDataPath`; its fallback is `<LocalApplicationData>/<EntryAssemblyNameNormalized>`. Use `GetPath(...)` for safe child paths.

### Nexus Keys

Explicitly configure `NexusAppSettings:AppId` (0 through 127) and `NexusAppSettings:AppInstanceId` (0 through 63) in every environment; zero is valid for both. Concurrent instances generating IDs for the same application require distinct AppInstanceIds. `Validate<TEntity>` uses the entity's declared `(EntityType, AppId)`, independently of the configured AppId. `Validate(id, entityType)` uses the configured partition; override it with `Validate<TApp>(id, entityType)` where `TApp : IAppId`, or `Validate(id, entityTypeId)` with an explicit `EntityTypeId`. Nullable inputs preserve null. Parsing and operations validation use `UseSecureSourceKnownIds` when the format is omitted; explicit Secure, Plain or Auto overrides it. [SharedKernel record validation](../drn-sharedkernel/SKILL.md#validation-approaches) checks stored metadata independently of configuration.

`NexusAppSettings.Keys` must contain exactly one default `NexusKey`. Generation uses the default key; parsing tries the default key first and then the remaining configured keys for rotation fallback.

`NexusKey.Format` defaults to `ByteEncoding.Utf8` and supports:

| Format | Requirement |
|--------|-------------|
| `Utf8` | Key is exactly 32 UTF-8 bytes. |
| `Hex` | Hex decodes to exactly 32 bytes, normally 64 hex chars. |
| `Base64` | Base64 decodes to exactly 32 bytes. |
| `Base64UrlEncoded` | Base64Url decodes to exactly 32 bytes. |


---

## Scoped Logging (IScopedLog)

Project-owned `data-classification-rules.json` extends diagnostic name rules through the shared parser. Register it as EmbeddedResource or AdditionalFiles. SharedKernel targets publish reference-assembly catalog metadata for downstream analyzers. Set `DiagnosticLogging:RuleAssemblyName` to load the application's own and referenced SharedKernel-consumer catalogs at runtime. For loose files, set RuleFile to the deployed path and add build/publish copy items. Relative file paths resolve from AppContext.BaseDirectory. Do not scan working directories. Leaving both options null preserves built-in-only behavior.

Invalid configured sources fail sanitizer creation. Runtime graph discovery skips FileNotFoundException only when resolving transitive references; missing configured roots, other load failures, and invalid catalogs still fail. Unavailable assemblies contribute no catalogs. Loaded rules are stable per options instance and snapshots; fresh options reload loose files, while embedded changes require rebuilding their assemblies. Secret/personal groups permit reviewed name exemptions; sensitiveFieldPaths and secretPathSegments remain non-exemptible. Existing runtime configured rules retain their precedence. Browser rules need explicit configuration. Follow the profile's classification maintenance workflow when adding a file or item. Cover inherited catalog scope, invalid sources, positive and false-positive fixtures, and release-note impact.

The shared catalog protects `cvv`, `cvc`, `pincode`, `username`, `firstname`, `lastname`, `dateofbirth`, `birthdate`, and `iban`. Do not add broad `otp` substring matching: it also matches `RootPath`. Use explicit classification for OTP properties. Preserve matching and nonmatching vectors across analyzer and browser/server fixtures.

Use plain `redacted` for protected URL path segments, query values, and named fragment values. Preserve bare fragments without content inspection. Keep `[redacted]` for ordinary diagnostic values, whole-field redaction, and form-body values. Preserve server/browser parity through shared fixtures.

JSON and form-body field matching includes the supplied source path and decoded field names. Keep URL query and fragment keys independent of the URL path. Cover configured source-path rules with equivalent JSON and form fixtures.

Request-scoped structured logging. Aggregates data, metrics, and checkpoints, flushing one fitting entry or bounded linked overflow records.

Dedicated metric methods exempt operation identifiers from built-in name rules with the native sanitizer. Configured rules still apply to the prefix plus operation name. Custom sanitizers retain their existing sensitivity checks. Use code-owned metric identifiers, never request values.

Native copies preserve metric provenance and accumulated values. Check destination rules against the original operation path and stored field name. Ordinary writes replace metric provenance. Copies to custom sanitizers must use redacted source snapshots, never internal capture wrappers.

AddDrnUtils registers DiagnosticRedactor separately for DataClassifications.Sensitive, DataClassifications.Personal and their combined Set. Registering the combined set alone does not cover individual classifications. Verify all three provider lookups.

DRN0019 exempts direct EF migration schema expressions containing column-operation metadata in column definitions or constraint selectors. Ordinary migration data and logging remain checked. Do not suppress diagnostics by migration path or generated-file marker.

DRN0017/DRN0020 distinguish direct NSubstitute Received/DidNotReceive verification chains, including WithAnyArgs variants, from captures. Ordinary substitute calls and independent declaration diagnostics remain checked. Verification aliases and wrappers are not inferred.

Diagnostic rules use normalized substring matching. Server named models use property names without logging-prefix matches. Anonymous data, dictionary/JSON nodes, and browser data retain structural matching. Configured server rules always use full paths. Keep shared catalog fixtures aligned.

Use AddDictionary/SanitizeDictionary for classified dictionary capture. DiagnosticKeyClassification applies Sensitive/Personal redaction or reason-bearing scalar-only NonSensitive exemptions to exact keys at one level. These APIs check own keys without built-in prefix matches, while configured full-path rules remain authoritative. DRN0020 redirects statically known matching dictionary captures, including nested ones and direct local writes. Aliases and interprocedural flow are not fully tracked. Foreign implementations fail closed. Preserve positive-classification precedence, native-copy provenance and budgets.

DRN0019 requires anonymous properties whose names match the shared catalog to move to named classes or records with classification or direct `[Sensitive(false)]`. This includes nested objects, inferred members and generated code, independently of capture calls. Harmless anonymous properties remain allowed. Reason strings and capture policies cannot bypass declaration analysis.

Secret field matching uses `secret`, not `sec`. Seconds, sections, and response-capture flags need no exemption solely for containing `sec`. Keep analyzer, runtime, and browser cases aligned with the shared catalog.

Object logging captures readable public instance properties only, including nested object projection. Omit public fields entirely, even when annotated. Keep name-based redaction for properties, dictionary keys, JSON properties, and explicitly named log entries. Preserve classified getter suppression.

DiagnosticTypeMetadata owns cached type classification and readable member plans. Existing service assembly discovery registers closed types in frozen lookups. Attribute and member construction stays lazy; unknown runtime types use the same cached fallback rules. Merge positive classifications and IgnoreLog from mapped interface properties for concrete and nested capture. Interface exemptions cannot waive concrete name rules, and unrelated explicit implementations must not classify same-named public properties. Keep source getter reflection and boxing claims separate from metadata lookup performance.

Native GetLogs materializes captured values under the storage lock without a preceding deep clone. It never invokes source model getters. CopyFrom retains its detached capture clone. DiagnosticJsonSize reuses thread-local writers with separate reentrant rentals and clears pooled discard buffers before return. Measure JSON escaping with the writer rather than estimating UTF-8 text length. Fragment search measures spans and allocates only accepted strings. Preserve byte bounds, snapshot isolation, and exact fragment reconstruction. No measured speedup or allocation-free capture claim is warranted without execution evidence.

Server and browser text capture preserves contents within budgets. Do not inspect text for assignments, credentials, emails, private-key blocks, embedded URLs, or known literals. Redaction uses field names, explicit URL structure, and classification attributes only.

DRN0015 through DRN0020 analyze generated sources without reporting diagnostics in generated code. Runtime classification remains active.

Use `[Sensitive]`, `[Personal]`, or derived attributes for type/member classification and `IDiagnosticSanitizer` for text, URL, body, and structured adapters. Other Microsoft non-None classifications retain runtime protection but do not satisfy DRN0015/DRN0018. All public readable instance non-indexer properties are analyzed, including ignored and generated properties, in projects receiving SharedKernel analyzers. `[IgnoreLog]` suppresses capture, never classification analysis. Direct property `[Sensitive(false)]` exempts that node from built-in name rules only. Positive classification and configured rules win. Inherited false annotations do not grant exemptions. Named models need no prefix exemptions. Prefer `Add(key, value, exemptionReason)` or `SanitizeValue(path, value, exemptionReason)` only for supported scalars, including nullable values, DateOnly, TimeOnly and Uri. Uri always uses structural URL sanitization. Complex reason-bearing captures fail closed. The reason must be application-authored and nonblank. Keep `DiagnosticCapturePolicy` for complex/nested exceptions. Never grant a subtree exemption. Preserve getter suppression, detached snapshots, native copy provenance including property-name decisions, and capture budgets. Public snapshots carry no exemption authority. `DiagnosticRuleCatalog` shares the canonical JSON and normalization with analyzers; `DiagnosticNameRules` caches bounded decisions per runtime policy. Reflection and serialization remain. Do not claim measured speedups without benchmarks. Use `LogScoped` and discover the source-owned diagnostic guide through the profile.

`ScopeEvent` uses `Id` (.NET `EventId`), `Outcome`, and `Reason`. `IScopedLog.WithEvent` sets the first event as primary and retains later events under `AdditionalEvents`. Scope-level properties retain the `Event` prefix for ID/name/outcome/reason. `LogScoped` forwards the primary event ID with existing severity rules. Every `ScopedLog` generates a stable `CorrelationId` and captures a nullable W3C `TraceId` at construction. Never fabricate a trace ID without an activity. HTTP TraceIdentifier remains separate. Native OpenTelemetry trace/span/flags use the activity at emission; scoped snapshots do not populate those native fields. Copying retains destination correlation and primary event ownership. Keep consumer/module catalogs separate; do not subclass event types.

| Method | Purpose |
|--------|---------|
| `Add(key, value)` | Structured log entry |
| `AddToActions(msg)` | Execution trail |
| `AddProperties(key, object)` | Flatten complex objects |
| `Measure(key)` | Capture duration & count |
| `AddException(ex, msg)` | Log exception with details |
| `Increase(key, by)` | Atomically increment counter |

**Auto-captured**: TraceId, Request (path/method/host/IP), Response (status/length), Exceptions, Duration.

---

## HTTP Clients (`IExternalRequest`, `IInternalRequest`)

Wrappers around [Flurl](https://flurl.dev/) with standardized JSON conventions.

```csharp
// External API
var response = await request.For("https://api.example.com", HttpVersion.Version11)
    .AppendPathSegment("v1/charges")
    .PostJsonAsync(new { Amount = 1000 })
    .FromJsonAsync<ExternalApiResponse>();

// Internal Service Mesh (Kubernetes/Linkerd)
public interface INexusRequest { IFlurlRequest For(string path); }

[Singleton<INexusRequest>]
public class NexusRequest(IInternalRequest request, IAppSettings settings) : INexusRequest
{
    public IFlurlRequest For(string path) => request.For(settings.NexusAppSettings.NexusAddress)
        .AppendPathSegment(path);
}
```

`InternalRequest` and `ExternalRequest` accept an optional `HttpMessageHandler?` via constructor dependency injection, enabling in-memory routing and test interception (such as `ApplicationContextRouterHandler`) without mutating global Flurl static state.

Buffered converters capture `HttpStatus` and `Payload`, then dispose the response even if reading fails. Use `using` for streaming wrappers; disposal releases the response and any `IDisposable` payload.
Converters are exposed as extension methods on `Task<IFlurlResponse>` and `IFlurlResponse`; `HttpResponse` only models the converted result and its ownership.

`HttpResponse.StatusClass` distinguishes `1xx`, `2xx`, `3xx`, `4xx`, and `5xx` responses, and `IsSuccessStatusCode` is true only for `2xx`. Flurl normally follows redirects, so `3xx` is observed only when a redirect remains final. Use `AllowAnyHttpStatus()` with the throwing converters to inspect non-success responses directly.

The `TryToStringAsync`, `TryToBytesAsync`, `TryToStreamAsync`, and `TryFromJsonAsync<T>` converters return `HttpCallResult<T>` for inspectable failures. HTTP status and processing failure are separate: readable `4xx`/`5xx` responses retain their status and payload without a `Failure`; transport, timeout, response-read, and deserialization errors populate `Failure`. A successful HTTP status with malformed JSON preserves the status but has `IsSuccess == false`. Try converters propagate cancellation. Dispose streaming try results, and treat `HttpFailure.Message` and `HttpFailure.Exception` as local diagnostic data that may contain request details and requires redaction before logging or exposure; both are ignored by System.Text.Json serialization.

Use `HttpCallResult<T>.ThrowIfFailure()` to rethrow a captured processing failure with its original stack after inspection. It does not throw for a `3xx`, `4xx`, or `5xx` result whose response was read successfully; status enforcement remains an explicit caller decision through `StatusClass` or `IsSuccessStatusCode`.

---

## ScopeContext (Ambient Context)

Access ambient data only after DRN Hosting initializes the current request scope or a test explicitly calls `ScopeContext.InitializeForTest(...)`. A `DrnTestContext` alone does not initialize `ScopeContext`; use injected services during startup, background work, console execution, and other uninitialized scopes.

`ScopeContext.Problem` lazily stores public error defaults (`Type`, `Title`, `Detail`, `Instance`, `Extensions`) without ASP.NET dependencies. Hosting fills missing response fields only. Do not copy logs or exception messages into it. The mutable object belongs to one scope, resets with test initialization, and must not coordinate separate concurrent errors.

`ScopeProblem` mirrors every `ExceptionFor` factory with the same arguments and throws immediately without changing public defaults. Hosting infers the standard problem type from the exception's status; assigning Type does not change status. See [Hosting mapping rules](../drn-hosting/SKILL.md#error-responses). `[StackTraceHidden]` hides helper frames from formatted traces without relocating the throw instruction. Examples show the scoped shorthand and equivalent `throw ExceptionFor` call together.

```csharp
ScopeContext.UserId;                      // Current user
ScopeContext.TraceId;                     // Request trace
ScopeContext.Authenticated;               // Auth status
ScopeContext.Settings;                    // Static IAppSettings
ScopeContext.Log;                         // Static IScopedLog
ScopeContext.IsUserInRole("Admin");       // Role check
ScopeContext.IsClaimFlagEnabled("FeatureX"); // Feature flag
```

---

## ID Generation

```csharp
long internalId = sourceKnownIdUtils.Next<User>();             // DB PK
SourceKnownEntityId externalId = sourceKnownEntityIdUtils.Generate<User>(internalId); // Public GUID

// Secure ↔ Plain conversion (idempotent)
var secureId = sourceKnownEntityIdUtils.ToSecure(entityId);
var plainId = sourceKnownEntityIdUtils.ToPlain(entityId);
```

`ISourceKnownEntityIdUtils` inherits `ISourceKnownEntityIdOperations` (SharedKernel), which defines `Generate`, nullable/nonnullable `Parse`, all `Validate` overloads, `ToSecure`, `ToPlain`. This interface is injected into entities by EF interceptors.

Entity ID `Parse` and all `Validate` overloads accept non-nullable `SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault`. The constructor sets read-only DefaultFormat to Secure when `UseSecureSourceKnownIds` is true, otherwise Plain. ConfiguredDefault selects this property; explicit formats remain unchanged. Secure only decrypts/verifies; Plain never decrypts; Auto retains plain-first detection and decryption fallback per key. Undefined formats throw even for null IDs; supported formats preserve null ID results. Conversions retain Auto revalidation. Replace null format arguments with ConfiguredDefault, rebuild consumers and update custom implementations/method-group bindings, including DefaultFormat.

Entity/repository GUID helpers forward the format to parsing, then check the parsed record's validity and identity. Record ValidateId(), Validate<TEntity>() and Validate(expected) have no format parameter and accept either parsed representation. Generated records also validate validity and identity. Records are publicly constructible and do not reauthenticate GUIDs; use them only as trusted internal values. Authenticate untrusted GUIDs through operations, which own keys and resolve configuration. Conversions reauthenticate the GUID using Auto.

Secure-only defaults block direct plaintext MAC guessing. Keep Plain/Auto overrides under application control, and validate external GUIDs with the required format before conversion. Authorization and rate limiting remain necessary. See [the security rationale](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Utils/README.md#parse--validation).

> [!NOTE]
> ID generation is automatically handled by `DrnContext` when SourceKnownEntities are saved.

### Entity Creation-Date Filters

`IEntityDateTimeUtils` filters `SourceKnownEntity.Id` by its 250ms Source-Known ID creation tick. Each date boundary maps to the minimum ID for that tick (payload bits cleared) and the maximum ID (all 31 app, instance, and sequence payload bits set).

| Filter | Inclusive boundary | Exclusive boundary |
|---|---|---|
| `CreatedAfter` | `Id >= tick.Min` | `Id > tick.Max` |
| `CreatedBefore` | `Id <= tick.Max` | `Id < tick.Min` |
| `CreatedBetween` | `Id >= begin.Min && Id <= end.Max` | `Id > begin.Max && Id < end.Min` |
| `CreatedOutside` | `Id <= begin.Max \|\| Id >= end.Min` | `Id < begin.Min \|\| Id > end.Max` |

`CreatedBetween` and `CreatedOutside` normalize reversed endpoints before applying thresholds. Equal endpoints therefore select the whole tick for inclusive `Between`, no rows for exclusive `Between`, all rows for inclusive `Outside`, and everything except the tick for exclusive `Outside`.

Keep the payload-mask calculation outside the query expression so providers receive scalar `long` comparisons. Unit regressions must cover inclusive/exclusive payload edges, equal and distinct endpoints, reversed inputs, and the negative-to-positive epoch-half transition. Use database-backed verification only when SQL translation or query-plan behavior is the subject.

---

## Scoped Cancellation

`ICancellationUtils` owns a root and keyed child scopes for the current DI service scope.

| Intent | Use | Propagation |
|---|---|---|
| Cancel all work | `cancellation.Root.Cancel()` / `.Merge(token)` | Every existing and later-created child. |
| Cancel a component/workflow | Child `.Cancel()` / `.Merge(token)` | That group only. |
| Cancel one operation | Local linked token source | Caller-owned work only. |

```csharp
private static readonly CancellationScopeKey ScopeKey =
    CancellationScopeKey.For<PaymentWorkflow>();

var scope = cancellation.GetOrCreateScope(ScopeKey);
scope.Merge(workflowLifetimeToken);
```

- The same key returns the same scope and token; canceled scopes cannot be reset.
- Every key requires a non-null name and may have an owning type. Prefer `For<T>()` or `For(Type)` for an empty-name group owned by one type. Use `For<T>(name)` or `For(Type, name)` when that type owns multiple intentional groups, and `For(name)` only when different types intentionally share one ownerless group.
- Names use ordinal, case-sensitive equality and must be developer-defined constants of at most 128 characters. Empty and whitespace names are permitted. Never use request data, user input, instance IDs, or operation IDs to create groups.
- Ownerless keys share one ordinal-name namespace within the current `ICancellationUtils` service scope. Although empty and whitespace names are valid, prefer qualified, centrally defined names because unrelated callers using the same ownerless name receive the same scope and can cancel each other's work.
- Keys are opaque and factory-created; the default value is invalid.
- `ICancellationUtils` owns child scopes. Callers own and dispose local linked sources used for operation-only cancellation.
- Replace removed root members with their `cancellation.Root` equivalents.

When maintaining cancellation internals:

- Keep the effective token stable across incremental merges. Detach external registrations after terminal cancellation.
- Mark the parent disposed and detach children under its lock. Cancel and dispose them outside the lock.
- During reentrant disposal from a root callback, defer child and root cleanup until root cancellation completes.
- A single linked source is suitable for fixed inputs. Dispose it only after all consumers finish.

See the upstream [Scoped Cancellation](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Utils/README.md#scoped-cancellation) guide for the complete API and migration example.

---

## Concurrency (`LockUtils`)

Lock-free atomic operations via `Interlocked`:

| Method | Purpose |
|--------|---------|
| `TryClaimLock(ref int)` | Atomically claim (0→1) |
| `TryClaimScope(ref int)` | Disposable auto-release scope |
| `ReleaseLock(ref int)` | Unconditionally release (→0) |
| `TrySetIfNull<T>(ref T?, T)` | CAS set-if-null |
| `TrySetIfEqual<T>(ref T?, T, T?)` | CAS when current is the same reference as comparand |
| `TrySetIfNotEqual<T>(ref T?, T, T?)` | Set if current is not the same reference as comparand (retry loop) |
| `TrySetIfNotNull<T>(ref T?, T)` | Set if current is not null |

```csharp
private int _lock;
using var scope = LockUtils.TryClaimScope(ref _lock);
if (scope.Acquired) { /* critical section */ }
```

---

## Utilities Reference

Use one `AuthenticationClaimConfig` for mapped consumers. Identity defaults have explicit aliases; custom mappings replace them. Subject agreement checks include only configured types and aliases, including case variants; unconfigured standard subject claims are ignored. Canonical types govern issuance/native metadata; aliases are additional DRN inputs and do not rewrite claims. Identity options and future handler integrations must supply matching native name/role claims and metadata while preserving issuer isolation and exact MFA evidence. See [the claim contract](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Utils/README.md#scope--ambient-context-scopecontext).

`TotpUtils` defaults to six digits, 30-second steps, and ±1-step drift. Verification is stateless; callers must enforce atomic per-account replay protection and attempt limits. It does not issue MFA claims. See upstream [TotpUtils.cs](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Utils/Auth/MFA/TotpUtils.cs) for parameter validation details.

`MfaPrincipal.IsRecent` and `IsPhishingResistant` are opt-in checks requiring an explicit trusted issuer and completed/additional evidence on the same authenticated identity. All authenticated subjects/issuers must be unambiguous and agree. IsRecent accepts a caller-supplied current time, an inclusive nonnegative age limit and a timestamp type (default auth_time); reject malformed/conflicting/future evidence. auth_time is authentication recency, not necessarily verified-MFA recency. IsPhishingResistant requires an explicit assurance mapping distinct from completion; never infer it from generic MFA or a passkey label. No default Hosting policy change or claim issuance; provider mapping remains MFA-07.

| Area | Key Types | Purpose |
|------|-----------|---------|
| **Data Encoding** | `EncodingExtensions`, `Base32Encoding` | Base64, Base64Url, Hex, Utf8, plus strict RFC 4648 padded/unpadded Base32 |
| **Authentication** | `AuthenticationClaimConfig`, `MfaClaimConfig`, `MfaPrincipal`, `MfaFor`, `TotpUtils` | One config carries canonical types, explicit aliases, and the MFA marker. Preserve exact evidence/account checks and default subjectless compatibility; assurance and Identity operations require subjects. No Identity-service dependency. |
| **Hashing** | `HashExtensions` | Blake3 (crypto), XxHash3 (fast), keyed and stream/file hashing; prefer stream overloads for files and large payloads |
| **JSON** | `JsonMergePatch` | RFC 7386 merge patch with depth protection |
| **Query Strings** | `QueryParameterSerializer` | Complex objects → query strings |
| **Streams** | `ToBinaryDataAsync` | Safe consumption with `MaxSizeGuard` |
| **Validation** | `ValidationExtensions` | DataAnnotations-based programmatic validation |
| **Validators** | `JpegValidator`, `JpegValidationResult`, `JpegValidationErrorReason` | Structural, stream-based, size-bounded JPEG validation with typed error reasons |
| **App data** | `IAppData`, `AppDataPathResult`, `DrnAppDataSettings` | Validated temp/data roots with traversal-safe child path resolution |
| **Pagination** | `IPaginationUtils` | Cursor-based via `SourceKnownEntityId` |
| **Cancellation** | `ICancellationUtils`, `ICancellationScope`, `CancellationScopeKey` | Explicit root, stable named groups with optional owners, and caller-owned local links |
| **Diagnostics** | `DevelopmentStatus` | Track pending DB model changes at startup |

### Bit Packing (`NumberBuilder` / `NumberParser`)

Zero-allocation bit manipulation for custom ID generation:

```csharp
var builder = NumberBuilder.GetLong();
builder.TryAddNibble(0x05);  // Add 4 bits
builder.TryAddUShort(65535); // Add 16 bits
long packed = builder.GetValue();

var parser = NumberParser.Get(packed);
byte nibble = parser.ReadNibble();
ushort value = parser.ReadUShort();
```

### Time & Async

Configure `SourceKnownIdSettings` before startup or first ID/epoch use. `DefaultEpoch` requires an explicit `MinimumUtc`; both freeze on first use, including historical conversion. Hosting validates before constructors/hooks; non-hosted generation validates on first use. See [the time contract](../drn-sharedkernel/SKILL.md#source-known-identity-system).

```csharp
// Cached UTC timestamp: 250ms precision, refreshed every 10ms
long timestamp = TimeStampManager.CurrentTimestamp();
DateTimeOffset now = TimeStampManager.UtcNow;

// Async-safe timer — prevents overlapping executions
await using var worker = new RecurringActionAsync(
    async ct => await DoWorkAsync(ct),
    periodMilliseconds: 1000, start: true,
    executionTimeout: TimeSpan.FromSeconds(10));
worker.Stop();
worker.Start(); // Resume after stopping
```

`RecurringActionAsync.Stop()` requests cancellation without waiting for the active callback to finish.

`RecurringAction` accepts synchronous `Action` callbacks only. Dedicated threads require a positive period; timer mode accepts zero. Dedicated restarts preserve the configured post-callback delay, including when restarting during an active callback. Synchronous disposal does not wait for active callbacks. `RecurringActionAsync` awaits callbacks; restarted execution and `DisposeAsync()` wait for prior callbacks to finish. Timeouts require token-aware callbacks and request cooperative cancellation; ignoring cancellation can block later iterations and async disposal. Tokenless `Func<Task>` constructors have no timeout parameter.

Validate `RecurringActionAsync` periods and timeouts during construction, including `start: false`. Periods truncate to 1–4,294,967,294 milliseconds; timeouts must be positive and truncate to at most that upper limit. Preserve positive sub-millisecond timeouts. See [timer usage and errors](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Utils/README.md#non-overlapping-async-timer-recurringactionasync).

`TimeProvider` singleton registered to `TimeProvider.System` by default for testable time.

### Extension Methods

```csharp
// ServiceCollectionExtensions
var replacement = new MyService();
services.ReplaceInstance<MyService>(typeof(IMyService), new[] { replacement }, ServiceLifetime.Singleton);
services.ReplaceTransient<IMyService, MyService>(replacement);
services.ReplaceScoped<IMyService, MyService>(replacement);
services.ReplaceSingleton<IMyService, MyService>(replacement);
services.GetAllAssignableTo<TService>();

// String & Binary
"hello world".ToStream();
int v = "123".Parse<int>();     bool ok = "abc".TryParse<int>(out _);
// Casing/path helpers live in DRN.Framework.SharedKernel.Extensions.

// Type & Assembly
assembly.GetTypesAssignableTo(typeof(TInterface));
assembly.GetSubTypes(typeof(T));
assembly.CreateSubTypes<T>(); // Discover + instantiate parameterless ctors
type.GetAssemblyName();

// Reflection (cached invokers - prefer UnsafeAccessor when compile-time types are known)
instance.InvokeMethod("Name", args);
type.InvokeStaticMethod("Name", args);
instance.InvokeGenericMethod("Name", typeArgs, args);

// Flurl & HTTP Diagnostics
await ex.PrepareScopeLogForFlurlExceptionAsync(scopedLog, appFeatures); // Captures request/response into IScopedLog
exception.GetGatewayStatusCode();              // Maps to 502/503/504

// Object & Dictionary
object.GetGroupedPropertiesOfSubtype(typeof(T));      // Group properties by subtype
number.GetBitPositions();                       // Get set bit positions
```

---

## UnsafeAccessors (Preferred Over Reflection)

Use `[UnsafeAccessor]` (`System.Runtime.CompilerServices`) instead of runtime reflection (`Type.GetMethod`, `Type.GetProperty`, `Type.GetField`, `MethodInfo.Invoke`) whenever the target type is accessible at compile time. `[UnsafeAccessor]` provides zero-overhead, AOT-safe, direct compiled access to non-public methods, fields, constructors, and properties.

Use the upstream source-owned signatures as examples: [FlurlExtensions](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Utils/Extensions/FlurlExtensions.cs) for field access, [DrnApplicationExtensions](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Hosting/DrnProgram/DrnApplicationExtensions.cs) for method access, and [PageUtils](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Hosting/Endpoints/PageUtils.cs) for property-setter access.

### When to Use UnsafeAccessor vs Reflection

- **Use `[UnsafeAccessor]`**: Whenever accessing non-public constructors, methods, properties, or fields of known/accessible types (including framework, ASP.NET Core, and library types). Note that `UnsafeAccessorKind.StaticMethod` requires the exact declaring type where the static method is defined (it does not traverse base types for static members).
- **Use Reflection**: Only when target types or generic signatures cannot be determined at compile time (e.g. dynamically discovered types by assembly name/attribute scan, or inaccessible internal types of third-party assemblies without `InternalsVisibleTo`).

---

## Related Skills

- [drn-domain-design.md](../drn-domain-design/SKILL.md) - Domain & Repository patterns
- [drn-sharedkernel.md](../drn-sharedkernel/SKILL.md) - Domain primitives
- [drn-hosting.md](../drn-hosting/SKILL.md) - Web hosting
- [drn-entityframework.md](../drn-entityframework/SKILL.md) - Database access
- [drn-testing.md](../drn-testing/SKILL.md) - Testing with contexts

---

## Global Usings

```csharp
global using DRN.Framework.SharedKernel;
global using DRN.Framework.Utils.DependencyInjection;
```
