[![master](https://github.com/duranserkan/DRN-Project/actions/workflows/master.yml/badge.svg?branch=master)](https://github.com/duranserkan/DRN-Project/actions/workflows/master.yml)
[![develop](https://github.com/duranserkan/DRN-Project/actions/workflows/develop.yml/badge.svg?branch=develop)](https://github.com/duranserkan/DRN-Project/actions/workflows/develop.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)

[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=bugs)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=ncloc)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=coverage)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)

# DRN.Framework.Utils

> Core utilities package providing attribute-based dependency injection, configuration management, scoped logging, ambient context, and essential extensions.

## TL;DR

- **Attribute DI** — `[Scoped<T>]`, `[Singleton<T>]`, `[Transient<T>]` for zero-config service registration
- **Configuration** — `IAppSettings` with typed access, `[Config("Section")]` bindings
- **App data roots** — `IAppData` resolves temp/data paths with traversal-safe child paths
- **Scoped Logging** — `IScopedLog` aggregates structured logs per request
- **Scoped Cancellation** — Explicit root cancel-all plus stable keyed groups with optional type ownership
- **AES-256 single block** — Explicit runtime-intrinsic and portable paths with automatic fallback
- **Validators** — Reusable payload validators such as `JpegValidator`
- **Monotonic Pagination** — Cursor-based pagination leveraging entity ID temporal ordering
- **Bit Packing** — `NumberBuilder` for compact custom data structures
- **Ambient Context** — `ScopeContext.UserId` and `ScopeContext.Settings` within initialized DRN Hosting request scopes
- **Auto-Registration** — `AddServicesWithAttributes()` scans and registers public, concrete attributed services
- **SourceKnownEntityId utilities** — generation, validation, secure/plain conversion, auto-detecting `Parse`

## Table of Contents

- [QuickStart: Beginner](#quickstart-beginner)
- [QuickStart: Advanced](#quickstart-advanced)
- [Setup](#setup)
- [Dependency Injection](#dependency-injection)
- [Configuration](#configuration)
- [Logging (IScopedLog)](#logging-iscopedlog)
- [HTTP Client Factories (IExternalRequest, IInternalRequest)](#http-client-factories-iexternalrequest-iinternalrequest)
- [Scope & Ambient Context (ScopeContext)](#scope--ambient-context-scopecontext)
- [Data Utilities](#data-utilities)
- [Validators](#validators)
- [Pagination](#pagination)
- [Bit Packing](#bit-packing)
- [Diagnostics](#diagnostics)
- [Time & Async](#time--async)
- [Concurrency](#concurrency)
- [Extensions](#extensions)
- [Suggested Consumer Global Usings](#suggested-consumer-global-usings)
- [Related Packages](#related-packages)

---

## QuickStart: Beginner

Register and use a service with attribute-based DI:

```csharp
// 1. Define your service with DI attribute
public interface IGreetingService { string Greet(string name); }

[Scoped<IGreetingService>]
public class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello, {name}!";
}

// 2. Register public, concrete attributed services in Startup
services.AddServicesWithAttributes();

// 3. Inject and use
public class HomeController(IGreetingService greetingService) : Controller
{
    public IActionResult Index() => Ok(greetingService.Greet("World"));
}
```

## QuickStart: Advanced

Complete example with configuration binding, scoped logging, and ambient context:

```csharp
// Bind configuration section to strongly-typed class
[Config]
public class PaymentSettings
{
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
}

// Service using scoped logging and settings
[Scoped<IPaymentService>]
public class PaymentService(IAppSettings settings, IScopedLog log, PaymentSettings config) : IPaymentService
{
    public async Task<PaymentResult> ProcessAsync(decimal amount)
    {
        // Track execution time
        using var duration = log.Measure("PaymentProcessing");
        
        // Add structured context
        log.Add("Amount", amount);
        log.AddToActions("Processing payment");
        
        // Access ambient data inside an initialized DRN Hosting request scope
        var userId = ScopeContext.UserId;
        
        // Use typed configuration
        if (config.TimeoutSeconds < 10)
            throw ExceptionFor.Configuration("Timeout too short");
        
        return new PaymentResult(Success: true);
    }
}
```

---

## Setup

> [!NOTE]
> If you are using `DRN.Framework.Hosting` (inheriting from `DrnProgramBase`), this package is **automatically registered and validated**.

For manual registration (e.g. Console Apps, Workers):

```csharp
// Registers attributes, HybridCache, and TimeProvider
builder.Services.AddDrnUtils();
```

`AddDrnUtils()` does not create an ambient `ScopeContext`; console and worker code should use injected services.

### HybridCache Registration

`AddDrnUtils()` registers Microsoft's `HybridCache` with default in-memory caching. To configure distributed caching (e.g., Redis), add your `IDistributedCache` registration before calling `AddDrnUtils()`:

```csharp
// Optional: Add distributed cache backend
builder.Services.AddStackExchangeRedisCache(options => 
{
    options.Configuration = "localhost:6379";
});

// HybridCache will use the distributed cache if available
builder.Services.AddDrnUtils();
```

For DRN Hosting rate limiting, use `HybridCache` to cache tenant plan, feature flag, or quota policy data. Do not treat `HybridCache` / `IDistributedCache` as an atomic distributed rate-limit counter by itself; hard multi-instance quotas need a backend designed for atomic operations, such as Redis with server-side Lua scripts, or enforcement at an API gateway/CDN/WAF layer.

## Dependency Injection

### Attribute-Based Registration

Reduce configuration boilerplate by using attributes directly on services. `AddServicesWithAttributes()` scans the calling assembly by default; pass an `Assembly` argument to scan a specific target assembly.

| Attribute | Lifetime | Usage |
|-----------|----------|-------|
| `[Singleton<T>]` | Singleton | `[Singleton<IMyService>] public class MyService : IMyService` |
| `[Scoped<T>]` | Scoped | `[Scoped<IMyService>] public class MyService : IMyService` |
| `[Transient<T>]` | Transient | `[Transient<IMyService>] public class MyService : IMyService` |
| `[SingletonWithKey<T>]` | Singleton (Keyed) | `[SingletonWithKey<IMyService>("key")]` |
| `[ScopedWithKey<T>]` | Scoped (Keyed) | `[ScopedWithKey<IMyService>("key")]` |
| `[TransientWithKey<T>]` | Transient (Keyed) | `[TransientWithKey<IMyService>("key")]` |
| `[HostedService]` | Singleton | `[HostedService] public class MyWorker : BackgroundService` |
| `[Config]` | Singleton | `[Config("Section")] public class MySettings` |
| `[ConfigRoot]` | Singleton | `[ConfigRoot] public class RootSettings` |

> [!NOTE]
> `[Singleton<T>]`, `[Scoped<T>]`, `[Transient<T>]`, and their keyed variants accept an optional `tryAdd` parameter (default: `true`). When `true`, `TryAdd` is used so existing registrations are not overwritten. Set it to `false` to allow multiple implementations of the same service type.

Assembly scan metadata is cached, while registration modules and startup-validation state remain isolated to each service collection and provider. Repeating registration for the same assembly on one service collection is idempotent.

### Hosted Services

Use `[HostedService]` to register `IHostedService`/`BackgroundService` implementations without manual `AddHostedService<T>()` calls. The class **must** implement `IHostedService`; otherwise the attribute is silently ignored.

```csharp
[HostedService]
public class MyBackgroundWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Do periodic work
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

### Validation & Testing

> [!NOTE]
> `DrnProgramBase` automatically runs this validation at startup.

- **Validation**: Ensure all registrations are resolvable via `ValidateServicesAddedByAttributesAsync()`.

```csharp
// In Program.cs
await app.Services.ValidateServicesAddedByAttributesAsync();
```

In integration tests with `DRN.Framework.Testing`:
```csharp
[Theory, DataInline]
public async Task Validate_Dependencies(DrnTestContext context)
{
    context.ServiceCollection.AddServicesWithAttributes(); // Register local assembly
    await context.ValidateServicesAsync(); // Verifies attribute-registered services can be resolved
}
```

### Scoped Cancellation

`ICancellationUtils` owns a root and keyed child scopes within the current DI service scope.

| Intent | API | Effect |
|---|---|---|
| Cancel all scoped work | `cancellation.Root.Cancel()` or `cancellation.Root.Merge(token)` | Reaches every existing and later-created child. |
| Cancel a component or workflow | `GetOrCreateScope(key).Cancel()` or `.Merge(token)` | Affects only that group. |
| Cancel one operation | A local linked `CancellationTokenSource` | Affects only caller-owned work. |

```csharp
public sealed class CheckoutWorkflow(ICancellationUtils cancellation)
{
    private static readonly CancellationScopeKey ScopeKey =
        CancellationScopeKey.For<CheckoutWorkflow>();

    public async Task RunAsync(
        CancellationToken workflowLifetimeToken,
        CancellationToken operationToken)
    {
        var scope = cancellation.GetOrCreateScope(ScopeKey);

        using var operationSource = CancellationTokenSource
            .CreateLinkedTokenSource(scope.Token, workflowLifetimeToken, operationToken);

        await SomeAsyncOp(operationSource.Token);
    }

    public void CancelWorkflow() => cancellation.GetOrCreateScope(ScopeKey).Cancel();

    public void CancelEverything() => cancellation.Root.Cancel();
}
```

The same key returns the same scope and token. Root cancellation reaches every child, while child cancellation does not affect the root or other groups. Canceled scopes cannot be reset.

Keys can be type-owned or ownerless. Prefer `CancellationScopeKey.For<T>()` for a compile-time type or `For(Type)` for a runtime type. Add a name with `For<T>(name)` or `For(Type, name)` when one type owns multiple intentional groups. Use `CancellationScopeKey.For(name)` only when different types intentionally share one group.

Names use ordinal, case-sensitive equality and must be non-null developer-defined constants of at most 128 characters (empty string and whitespace are permitted). Keys are opaque and factory-created; the default value is invalid.

Ownerless keys share one ordinal-name namespace within the current `ICancellationUtils` service scope. Although empty and whitespace names are valid, prefer qualified, centrally defined names such as `"MyPackage.CheckoutShutdown"`, because unrelated callers using the same ownerless name receive the same scope and can cancel each other's work.

Do not derive keys from request data, user input, instance IDs, or operation IDs because these values represent individual work rather than shared component or workflow lifetimes. `ICancellationUtils` owns returned scopes; callers own and dispose local linked sources.

For root-wide migration, replace `cancellation.Cancel()`, `Merge(token)`, `Token`, and `IsCancellationRequested` with their `cancellation.Root` equivalents.

### Module Registration & Startup Actions

Services can require complex registration logic or post-startup actions. Attributes inheriting from `ServiceRegistrationAttribute` handle this.

**Example**: `DrnContext<T>` (in `DRN.Framework.EntityFramework`) is decorated with `[DrnContextServiceRegistration]`, which:
1.  Registers the DbContext.
2.  Runs startup migration handling; Development auto-migration occurs when `DrnDevelopmentSettings:AutoMigrateDevelopment` is enabled (default: `true`).

```csharp
// The base class DrnContext handles the registration attributes.
// You just inherit from it, and your context is auto-registered with migration support.
public class MyDbContext : DrnContext<MyDbContext> { }
```

## Configuration

### IAppSettings

Access configuration using strongly-typed environment checks and utility methods.

```csharp
public class MyService(IAppSettings settings)
{
    public void DoWork()
    {
        if (settings.IsDevelopmentEnvironment) { /* dev-only logic */ }
        if (settings.IsStagingEnvironment) { /* staging-only logic */ }
        
        var conn = settings.GetRequiredConnectionString("Default");
        var value = settings.GetValue<int>("MySettings:Timeout", 30);
        var debugSummary = settings.GetDebugView().ToSummary(); // best-effort key-name redaction
    }
}
```

`GetDebugView(includeRawValues: true)` only includes raw values in Development. Summaries apply best-effort key-name redaction, not a complete security boundary; review them before logging or exposure. Child keys remain listed even when a provider also defines a scalar value for the parent section, and summary paths use the value provider's key casing. Object-based configuration helpers serialize through the framework JSON defaults and therefore use camelCase keys; explicit key/value configuration preserves the key text supplied by the caller.

### Configuration Attributes (`[Config]`)

Bind classes directly to configuration sections. These are registered as **Singletons**.

```csharp
[Config("PaymentSettings")] // Binds to "PaymentSettings" section
public class PaymentOptions 
{ 
    public string ApiKey { get; set; }
}

[Config] // Binds to "FeatureFlags" section (class name)
public class FeatureFlags { ... }

[ConfigRoot] // Binds to root configuration
public class RootSettings { ... }
```

### Configuration Sources

The framework automatically loads configuration in this order:
1.  `appsettings.json`
2.  `appsettings.{Environment}.json`
3.  User Secrets when the application assembly is available
4.  Environment variables (`ASPNETCORE_`, `DOTNET_`, then unprefixed)
5.  **Mounted Settings**:
    -   `/appconfig/key-per-file-settings/*`
    -   `/appconfig/json-settings/*.json`
6.  Command-line arguments

`Environment` is required and must be `Development`, `Staging`, or `Production`. DRN validates the value used to select `appsettings.{Environment}.json`; define it in `appsettings.json`, environment variables, mounted settings, or command-line arguments, and do not override it in environment-specific JSON or user secrets.

Override the mount directory by registering `IMountedSettingsConventionsOverride`.

### IAppSettings Troubleshooting

| Symptom | Cause | Solution |
|---------|-------|----------|
| `ConfigurationException` on startup | Missing or invalid required configuration | Inspect the reported key and correct its source value |
| `Environment setting is missing` | Required `Environment` key not configured | Set `Environment` to `Development`, `Staging`, or `Production` in `appsettings.json`, environment variables, mounted settings, or command-line arguments |
| `GetRequiredConnectionString` throws | Connection string not found | Verify key exists under `ConnectionStrings` section |
| `IsDevelopmentEnvironment` always false | Resolved `Environment` is not `Development` | Set the `Environment` configuration key to `Development` in an applicable source |
| Mounted settings not loading | Wrong mount path | Verify files exist at `/appconfig/json-settings/` or override via `IMountedSettingsConventionsOverride` |
| Environment variables not binding | Wrong naming format | Use `__` (double underscore) for nested keys: `MySection__MyKey` |

### App Data Settings

`DrnAppDataSettings` controls required temp/data roots. Overrides use process environment variables because roots resolve before DRN configuration.

| Environment variable | Purpose |
|---|---|
| `DrnAppDataSettings__TempPath` | Overrides the temp base; the resolved temp path is `<TempPath>/<EntryAssemblyNameNormalized>`. |
| `DrnAppDataSettings__DataPath` | Overrides the resolved data root as `<DataPath>`; the resolved temp path is `<DataPath>/Temp/<EntryAssemblyNameNormalized>` when temp is unset. |

Set `DrnAppDataSettings:RequireTemp` or `DrnAppDataSettings:RequireData` to fail startup when the resolved path is not valid.

### DrnAppFeatures

Feature flags and runtime knobs bound from the `DrnAppFeatures` configuration section via `[Config]`.

```json
{
  "DrnAppFeatures": {
    "SeedData": false,
    "SeedKey": "Peace at home! Peace in the world! - Mustafa Kemal Atatürk (1931)",
    "DisableRequestBuffering": false,
    "MaxRequestBufferingSize": 0,
    "DrnRateLimit": {
      "Disabled": false,
      "TokenLimit": 100,
      "ReplenishmentSeconds": 60,
      "TokensPerPeriod": 100,
      "PreAuthTokenLimit": 1000,
      "PreAuthReplenishmentSeconds": 60,
      "PreAuthTokensPerPeriod": 1000,
      "PostAuthTokenLimit": 0,
      "PostAuthReplenishmentSeconds": 0,
      "PostAuthTokensPerPeriod": 0
    }
  }
}
```

`DrnRateLimit` is the configuration key; application code reads the same settings through `IAppSettings.Features.RateLimit`.
Shared values apply to both DRN Hosting rate limiting phases. Phase-specific values set to `0` inherit the shared value; positive phase-specific values override it. Treat these values as global defaults; tenant plan, feature-flag, and account-specific quotas belong in DRN Hosting rate-limit rules. See the [Hosting README rate limiting settings](../DRN.Framework.Hosting/README.md#settings-quick-reference) for operational guidance, endpoint metadata behavior, and production scaling notes.

Nested option objects must be validated explicitly before relying on child data annotations for startup safety. `DrnAppFeatures` validates `DrnRateLimit` as part of root validation because plain `Validator.TryValidateObject` does not recursively walk nested objects by itself.

`DrnAppFeatures.SeedKey` feeds `AppSecuritySettings`. `AppSecuritySettings` derives `AppHashKey`, `AppEncryptionKey`, `AppKey`, and `AppSeed` through BLAKE3 derive-key mode with distinct DRN Framework context strings. `AppHashKey` and `AppEncryptionKey` remain Base64Url-encoded 32-byte values, `AppKey` remains an 8-character public discriminator, and `AppSeed` remains a signed 64-bit seed value. Changing `SeedKey` changes app-specific names, rate-limit keyed hash outputs, Development default Nexus key material, and seed-dependent operations. `AppSettings` enforces strict security constraints on `SeedKey` at startup:
- The built-in default `SeedKey` (`DrnAppFeatures.DefaultSeedKey`) is only permitted in the `Development` environment and is rejected in `Staging`, `Production`, or `NotDefined`.
- The sample `SeedKey` (`DrnAppFeatures.SampleSeedKey`) is only permitted during test execution (`TestEnvironment.DrnTestContextEnabled == true`) and is rejected in all non-test application runs.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ApplicationStartedBy` | `string?` | `null` | Identifies which test started the application (set automatically by `DrnTestContext`). |
| `SeedData` | `bool` | `false` | Enables data seeding on startup. |
| `SeedKey` | `string` | `"Peace at home!…"` | Secret key for seed operations. Enforced `[SecureKey(MinLength = 58)]`. Built-in default is permitted only in `Development`; sample key is permitted only during test execution. |
| `InternalRequestHttpVersion` | `string` | `"1.1"` | HTTP version used by `IInternalRequest`. |
| `InternalRequestProtocol` | `string` | `"http"` | Protocol scheme used by `IInternalRequest` (e.g., `http`, `https`). |
| `UseMonotonicDateTimeProvider` | `bool` | `false` | Reserved experimental flag for monotonic time-provider behavior data; it is not wired as a provider switch. |
| `DisableRequestBuffering` | `bool` | `false` | Disables request body buffering entirely. Use for high-throughput services (e.g., file upload endpoints). |
| `MaxRequestBufferingSize` | `int` | `0` (→ 30,000) | Maximum request body size to buffer in bytes. Values below 10,000 are ignored; 0 uses the 30,000-byte default. |
| `DrnRateLimit.Disabled` | `bool` | `false` | Disables both pre-auth and post-auth DRN Hosting rate limiting layers. |
| `DrnRateLimit.PartitionLogMode` | `RateLimitPartitionLogMode` | `KeyedHash` | Controls rejected IP/partition logging. `KeyedHash` logs deterministic keyed hashes for correlation; `PlainText` logs raw values and should be limited to controlled development or dedicated audit sinks. |
| `DrnRateLimit.TokenLimit` | `int` | `100` | Token bucket burst capacity. Must be positive. |
| `DrnRateLimit.ReplenishmentSeconds` | `int` | `60` | Token replenishment period in seconds. Must be positive. |
| `DrnRateLimit.TokensPerPeriod` | `int` | `100` | Tokens added per replenishment period. Must be positive. |
| `DrnRateLimit.PreAuthTokenLimit` | `int` | `1000` | Coarse pre-auth burst capacity for shared B2B NAT/VPN/CDN egress addresses. 0 inherits `TokenLimit`. |
| `DrnRateLimit.PreAuthReplenishmentSeconds` | `int` | `60` | Pre-auth replenishment period. 0 inherits `ReplenishmentSeconds`. |
| `DrnRateLimit.PreAuthTokensPerPeriod` | `int` | `1000` | Pre-auth tokens per period. 0 inherits `TokensPerPeriod`. |
| `DrnRateLimit.PostAuthTokenLimit` | `int` | `0` | Optional post-auth burst capacity. 0 inherits `TokenLimit`. |
| `DrnRateLimit.PostAuthReplenishmentSeconds` | `int` | `0` | Optional post-auth replenishment period. 0 inherits `ReplenishmentSeconds`. |
| `DrnRateLimit.PostAuthTokensPerPeriod` | `int` | `0` | Optional post-auth tokens per period. 0 inherits `TokensPerPeriod`. |

> [!TIP]
> Request buffering and rate limiting settings are consumed by `DRN.Framework.Hosting`. See the [Hosting README](../DRN.Framework.Hosting/README.md) for middleware details.

### NexusAppSettings and Nexus Keys

`NexusAppSettings` provides Nexus routing, source-known ID generator instance configuration, secure/plain ID mode, and the Nexus key ring used by `SourceKnownEntityIdUtils`. Domain entity ID generation automatically derives application partition identity (`AppId`) from entity `[EntityType<TApp>]` metadata, while configured `NexusAppSettings.AppId` defines the host's client routing partition and host domain partition alignment in Entity Framework.

```json
{
  "NexusAppSettings": {
    "MacType": "blake3",
    "NexusAddress": "localhost:5988",
    "AppId": 5,
    "AppInstanceId": 12,
    "UseSecureSourceKnownIds": true,
    "Keys": [
      {
        "KeyMaterial": "0123456789abcdef0123456789abcdef",
        "Format": "Utf8",
        "Default": true
      }
    ]
  }
}
```

`Keys` must contain exactly one default key. Generation always uses the default key. Parsing tries the default key first and then the remaining configured keys, so old IDs remain parseable during key rotation while the previous key stays in the key ring.

| `ByteEncoding` | Requirement |
|---------------------|-------------|
| `Utf8` | Default when omitted. `KeyMaterial` must be exactly 32 UTF-8 bytes. ASCII 32-character values satisfy this; non-ASCII values are valid only when the UTF-8 byte count is exactly 32. |
| `Hex` | `KeyMaterial` must hex-decode to exactly 32 bytes, normally 64 hex characters. A 32-character hex string is rejected because it decodes to 16 bytes. |
| `Base64` | `KeyMaterial` must Base64-decode to exactly 32 bytes. |
| `Base64UrlEncoded` | `KeyMaterial` must Base64Url-decode to exactly 32 bytes. This is the format used by Development default key-material generation. |

Invalid user-provided keys are not hashed, stretched, truncated, repaired, or treated as another format. Startup validation rejects malformed encodings, empty keys, wrong decoded lengths, and raw values that are not exactly 32 UTF-8 bytes. Exception messages avoid including the secret key value.


When no default Nexus key is configured in the `Development` environment, `AppSettings` derives deterministic 32-byte key material from `AppSecuritySettings` context-derived values with BLAKE3 derive-key mode. `DrnAppFeatures.SeedKey` feeds `AppSecuritySettings`. The generated key material is not random, is stored in memory as `Format = Base64UrlEncoded`, and then goes through the same BLAKE3 derive-key separation as configured keys.

## Logging (`IScopedLog`)

`IScopedLog` aggregates structured operational data, metrics, checkpoints, and exceptions for a logical scope. In DRN Hosting request scopes, Hosting enriches and emits that aggregate as a single log entry.

### Core Features

*   **Contextual**: Every `ScopedLog` has a stable `CorrelationId` and captures an active W3C `TraceId` when available. Hosting adds request and user context.
*   **Aggregation**: Groups all actions, metrics, and exceptions into a single structured log entry.
*   **Performance Tracking**: Built-in measurement for code block durations and execution counts.
*   **Exception Recording**: `AddException` records exception details without changing control flow; callers remain responsible for recovery, rethrowing, and excluding sensitive data.

### API Usage

#### Scope events

`ScopeEvent` has `Id`, `Outcome`, and `Reason` properties. `Id` uses .NET's `EventId` type from `Microsoft.Extensions.Logging`, which holds a numeric ID and an optional name.

```csharp
using DRN.Framework.Utils.Logging;
using Microsoft.Extensions.Logging;

public static class OrderLogEvents
{
    public static readonly EventId OrderProcessed = new(1, nameof(OrderProcessed));
}
```

Inside an operation with an injected `IScopedLog`:

```csharp
log.WithEvent(new ScopeEvent(OrderLogEvents.OrderProcessed, "success", "completed"));
```

`WithEvent` sets the first event as primary. `EventId`, `EventName`, `EventOutcome`, and `EventReason` expose it directly and appear in `GetLogs()`. Later calls retain their `ScopeEvent` values under `AdditionalEvents` without replacing the primary event. `LogScoped` passes the primary .NET `EventId` to `ILogger` and preserves exception/warning severity.

`CopyFrom` keeps destination correlation, trace, and primary event ownership. Source events are retained as additional events when a primary already exists.

`GetLogs` detaches action and additional-event lists under the writer lock. Later additions do not change earlier snapshots. `CopyFrom` uses the same list snapshot. Objects stored inside lists remain caller-owned; this is not a deep clone.

#### OpenTelemetry correlation

`TraceId` captures `Activity.Current.TraceId` at scope construction when a W3C activity exists. Otherwise it is `null` and omitted from `GetLogs()`. `CorrelationId` is always generated for the scope. HTTP `TraceIdentifier` stays separate. These values remain stable; creating a log does not start or export a trace.

With the OpenTelemetry logging provider configured, native log `TraceId`, `SpanId`, and `TraceFlags` come from the activity active when the log is emitted. Emit within that activity for native correlation. The scoped snapshot does not populate native trace fields or restore ended activities. See [OpenTelemetry log correlation](https://opentelemetry.io/docs/languages/dotnet/logs/correlation/).

Use module-owned catalogs such as `OrderLogEvents` with stable IDs and names. IDs are unique within a module, not across all libraries. Filter dedicated events by logger category and ID. Scope events share this model through composition, not inheritance.

#### Operation data

`AddProperties` reads public instance getters and skips indexers. Ignored properties are marked without invoking their getters. Other getter exceptions still propagate.

```csharp
public class OrderService(IScopedLog logger)
{
    public void ProcessOrder(int orderId)
    {
        // 1. Measure execution time and count
        using var _ = logger.Measure("ProcessOrder"); 
        
        // 2. Add structured data (Key-Value)
        logger.Add("OrderId", orderId); 
        logger.AddIfNotNullOrEmpty("Referrer", "PartnerA");

        // 3. Track execution checkpoints
        logger.AddToActions("Validating order"); 
        
        try 
        {
            // ... logic ...
            // 4. Flatten and add complex objects that are safe to log
            logger.AddProperties("User", new { Name = "John", Role = "Admin" });
        }
        catch(Exception ex)
        {
            // 5. Record the exception, then preserve failure semantics
            logger.AddException(ex, "Failed to process order");
            throw;
        }
    }
}
```

## HTTP Client Factories (`IExternalRequest`, `IInternalRequest`)

Wrappers around [Flurl](https://flurl.dev/) for HTTP clients with standardized JSON conventions and HTTP version policy configuration. Retries/circuit breakers are not configured by this package.

### External Requests

Use `IExternalRequest` for standard external API calls. It pre-configures `DefaultJsonSerializer` and enforces HTTP version policies.

```csharp
public class PaymentService(IExternalRequest request)
{
    public async Task Process()
    {
        // Enforces exact HTTP version for better compatibility with modern APIs
        var response = await request.For("https://api.example.com", HttpVersion.Version11)
            .AppendPathSegment("v1/charges")
            .PostJsonAsync(new { Amount = 1000 })
            .FromJsonAsync<ExternalApiResponse>();
    }
}
```

### Internal Requests (Service Mesh)

Use `IInternalRequest` for Service-to-Service communication in Kubernetes. It's designed to work with Linkerd, supporting automatic protocol switching (HTTP/HTTPS) based on infrastructure settings.

#### Recommended Pattern: Request Wrappers

Instead of using `IInternalRequest` directly in business logic, wrap it in a typed request factory for better maintainability and configuration encapsulation.

```csharp
// 1. Definition (External Factory Wrapper)
public interface INexusRequest { IFlurlRequest For(string path); }

[Singleton<INexusRequest>]
public class NexusRequest(IInternalRequest request, IAppSettings settings) : INexusRequest
{
    private readonly string _nexusAddress = settings.NexusAppSettings.NexusAddress;
    public IFlurlRequest For(string path) => request.For(_nexusAddress).AppendPathSegment(path);
}

// 2. Client Usage
public class NexusClient(INexusRequest request) : INexusClient
{
    public async Task<HttpResponse<string>> GetStatusAsync() =>
        await request.For("status").GetAsync().ToStringAsync();
}
```

### Primary Handler Injection

`InternalRequest` and `ExternalRequest` accept an optional `HttpMessageHandler?` via constructor dependency injection. When an `HttpMessageHandler` is registered in DI (such as `ApplicationContextRouterHandler` during integration testing), `FlurlClient` instances use the injected handler for in-memory routing and interception without modifying global static Flurl state.

Buffered response converters (`ToStringAsync`, `ToBytesAsync`, and `FromJsonAsync`) capture `HttpStatus` and `Payload`, then dispose the response even if reading or deserialization fails.
Call converters as extension methods on `Task<IFlurlResponse>` or `IFlurlResponse`; `HttpResponse` models the converted result and no longer exposes static conversion entry points.

`HttpResponse.StatusClass` classifies the status as `Informational`, `Success`, `Redirection`, `ClientError`, `ServerError`, or `Unknown`; `IsSuccessStatusCode` is true only for `2xx`. Flurl normally follows redirects, so a `3xx` snapshot represents a redirect that remained final. Use `AllowAnyHttpStatus()` when the throwing converters should return non-success responses for direct inspection.

Use the `TryToStringAsync`, `TryToBytesAsync`, or `TryFromJsonAsync<T>` counterparts when transport, timeout, response-read, or deserialization failures must be inspected without catching exceptions:

```csharp
var result = await request.For("status").GetAsync().TryFromJsonAsync<StatusResponse>();
if (result.Failure is { } failure)
{
    // Apply failure policy here; Payload may be unavailable.
    logger.LogWarning("HTTP conversion failed: {Kind}", failure.Kind);
}

if (result.StatusClass == HttpStatusClass.ClientError)
{
    // Handle 4xx. HttpStatus and a successfully converted Payload remain available.
}
else if (result.StatusClass == HttpStatusClass.ServerError)
{
    // Handle 5xx according to the caller's retry policy.
}
```

HTTP error statuses and processing failures are independent. For example, a `422` with readable JSON has `StatusClass.ClientError` and no `Failure`, while malformed JSON returned with `200` has `StatusClass.Success`, `IsSuccess == false`, and `Failure.Kind == Deserialization`. Try converters propagate cancellation. `HttpFailure.Message` and `HttpFailure.Exception` are available for local diagnostics, may contain request details, and must be redacted before logging or exposure; both are ignored by System.Text.Json serialization.

Call `result.ThrowIfFailure()` after inspection to rethrow a captured transport, timeout, response-read, or deserialization exception with its original stack preserved. The method does not throw for a `3xx`, `4xx`, or `5xx` response without a processing failure; inspect `StatusClass` or `IsSuccessStatusCode` when status enforcement is required.

Use `using` for streaming responses so the payload and response are released together:

```csharp
using var response = await request.For("export").GetAsync().ToStreamAsync();
await response.Payload!.CopyToAsync(destination);
```

`HttpResponse<T>.Dispose()` is idempotent and disposes any `IDisposable` payload.
`TryToStreamAsync()` transfers the same ownership to `HttpCallResult<T>`; dispose that result after consuming its stream payload.

## Scope & Ambient Context (ScopeContext)

`ScopeContext` provides ambient access to request-scoped data after a DRN Hosting request scope has been initialized. Use injected services during startup, in background work, and outside request scopes.

*   **Contextual Identity**: Access `UserId`, `TraceId`, and `Authenticated` status within an initialized request scope.
*   **Static Accessors**: Provides request-scope access to `IAppSettings`, `IScopedLog`, and `IServiceProvider`.
*   **RBAC Helpers**: Built-in support for role and claim checks.
*   **Test Initialization**: `ScopeContext.InitializeForTest(...)` resets the async-local scope before seeding test services, user, log, and trace data.

`IScopedUser` exposes authenticated identity and claim state. Use `GetClaimParameter<TValue>` for typed claims; `ScopeContext.GetClaimParameter<TValue>` provides ambient access to the same contract.

`AuthenticationClaimConfig` is the shared claim contract. Hosting registers the result of `DrnProgramBase.ConfigureAuthenticationClaims()` once; ordinary Identity applications need no override. Standalone helpers use `AuthenticationClaimConfig.Default`. `Subject`, `Name`, `Email`, and `Roles` each expose a canonical `Type` and immutable `Aliases`; `Mfa` identifies one exact completed-MFA type/value.

| Mapping | Default canonical type | Explicit aliases |
| --- | --- | --- |
| Subject | `ClaimTypes.NameIdentifier` | `sub` |
| Name | `ClaimTypes.Name` | `name` |
| Email | `ClaimTypes.Email` | `email` |
| Roles | `ClaimTypes.Role` | `roles` |
| Mfa | `amr=mfa` | None |

`Subject = new("uid")` replaces the entire mapping, accepting only `uid`. Add aliases explicitly, for example `new("uid", "external_id")`. Scalar aliases must agree; roles combine only selected types. Standard subject claims still veto conflicting values or issuers, including case variants; they are not fallback inputs to a custom mapping. `ScopedUser.Id` is null for missing or conflicting primary account evidence. Generic claim lookup remains case-insensitive; mapped security decisions use exact types.

Scoped name/email use the primary identity and return null for conflicting selected values or issuers. `IsInRole` checks all selected roles from authenticated identities across issuers. Generic claim groups retain their issuer filters.

Canonical types govern issuance and native `NameClaimType`/`RoleClaimType`; aliases are additional DRN inputs and do not rewrite claims or alter native authorization. Hosting configures Identity's claim options so its factory emits matching claims and metadata directly. Future authentication integrations must produce the same contract, mapping aliases into canonical claims only when needed, rejecting ambiguous evidence and excluding unselected case variants that native lookups could accept. Only validated identities belong in the application principal. Preserve claim provenance and identity boundaries; never infer MFA from `otp`, arbitrary `acr`, or a claim's name. See [Hosting integration](../DRN.Framework.Hosting/README.md#renewal-and-assurance).

Scoped users, `MfaFor`, `MfaPrincipal`, and Hosting authorization share the same config without requiring Identity services. Setup/pending credentials cannot prove MFA; multiple authenticated identities must agree on subject and issuer. The default subject mapping retains single-identity subjectless completion and same-object proof compatibility, including equivalent copies of the default mapping. Custom subject mappings, stronger assurance, Identity operations, and renewal require account evidence. Evaluate the final authorized `User` for account-security decisions.

For stronger opt-in checks, `MfaPrincipal.IsRecent(principal, config, trustedIssuer, maximumAge, utcNow, authenticationTimeClaimType)` and `IsPhishingResistant(principal, config, trustedIssuer, assuranceClaim)` require the completed marker and additional evidence on the same authenticated identity, from the specified issuer. All authenticated identities must have an unambiguous matching subject and issuer. Setup/pending credentials, missing subjects and untrusted evidence fail closed. `IsCompleted` and the default Hosting `Mfa` policy retain their current semantics.

`IsRecent` defaults to `auth_time`, accepts integer Unix seconds, rejects future/malformed/conflicting timestamps, and includes the exact maximum-age boundary. Pass the current time from `TimeProvider.GetUtcNow()`; a negative maximum age is invalid configuration. Authentication recency is not necessarily MFA recency: use a provider-guaranteed verified-MFA timestamp claim when that is the requirement. Renewal preserves existing `auth_time`; these helpers do not issue claims.

`IsPhishingResistant` requires an explicit `MfaClaimConfig` for an assurance marker distinct from the completed marker. Configure it only for an issuer/value that guarantees phishing-resistant authentication; generic `amr=mfa` and passkey labels do not automatically establish this. Provider validation/mapping and preservation of additional assurance claims remain application responsibilities. Missing assurance after renewal returns false.

`ScopeData` is separate caller-owned ambient storage and is not automatically copied into `IScopedLog`. Use `SetFlag` and typed `SetParameter` values for validated application data.

```csharp
var currentUserId = ScopeContext.UserId;
var traceId = ScopeContext.TraceId;
var settings = ScopeContext.Settings; // Static IAppSettings access
var logger = ScopeContext.Log; // Static IScopedLog access

if (ScopeContext.IsUserInRole("Admin")) { ... }

var tenantId = ScopeContext.GetClaimParameter<Guid>("tenant-id");
ScopeContext.Data.SetFlag("show-preview", true);
ScopeContext.Data.SetParameter("page-size", 50);
```

## TOTP Generation and Verification

`TotpUtils.GenerateTotpCode(sharedKey)` generates an authenticator code from a Base32 shared secret; `TotpUtils.VerifyTotpCode(sharedKey, code)` checks a submitted code. Defaults are six digits, 30-second steps, and ±1-step verification drift. Overloads accept an explicit timestamp and custom settings.

Verification is stateless: callers must enforce atomic per-account replay protection and attempt limits before accepting authentication. Bounded clock drift does not prevent code reuse. The utility does not issue MFA claims. See [TotpUtils.cs](Auth/MFA/TotpUtils.cs) for parameter validation details.

## Data Utilities

### App Data Roots (`IAppData`)

`IAppData` exposes validated temp/data roots. Normal startup recreates temp; DRN test contexts preserve sibling test data.

```csharp
public class ExportService(IAppData appData)
{
    public string GetExportPath(string fileName) =>
        appData.Temp.GetPath("exports", fileName);
}
```

Use `AppDataPathResult.GetPath(...)` for traversal-safe child paths.

### Encodings (`EncodingExtensions`)

Unified API for binary-to-text encodings and model serialization-encoding.
*   **Encodings**: Base64, Base64Url (Safe for URLs), Hex, and Utf8.
*   **Integrated**: `model.Encode(ByteEncoding.Hex)` and `hexString.Decode<TModel>(ByteEncoding.Hex)`.

`Base32Encoding` provides strict RFC 4648 Base32 encoding and decoding separately from `ByteEncoding`. Encoding produces canonical padded output by default and supports unpadded output for protocols such as authenticator shared keys. Decoding accepts canonical padded or unpadded input case-insensitively and rejects invalid lengths, padding, characters, and non-zero trailing bits.

```csharp
var encoded = Base32Encoding.Encode(bytes);
var unpadded = Base32Encoding.Encode(bytes, includePadding: false);
var decoded = Base32Encoding.Decode(unpadded);
```

### AES-256 Single-Block Encryption (`Aes256`)

`Aes256` accepts and returns one `Vector128<byte>` block. It exposes explicit x86/ARM runtime-intrinsic and portable .NET AES paths; the default `Encrypt` and `Decrypt` methods select runtime intrinsics when available and otherwise use the portable provider. Construction therefore remains portable, while explicit runtime-intrinsic methods throw `PlatformNotSupportedException` on unsupported hosts.

| Methods | Implementation |
|---|---|
| `Encrypt` / `Decrypt` | Runtime intrinsics with automatic portable fallback |
| `EncryptRuntimeIntrinsics` / `DecryptRuntimeIntrinsics` | Explicit x86 AES-NI or ARM AES intrinsics |
| `EncryptWithFramework` / `DecryptWithFramework` | Explicit cross-platform .NET AES provider |

A live instance supports concurrent calls. Intrinsic operations read pre-expanded round keys without locks or per-call allocation. On .NET 10.0.10, framework-provider operations are also lock-free because each call creates its own cipher state without mutating the configured key. Reverify this framework-provider assumption when changing the target runtime. Dispose the instance after all callers finish to clear the intrinsic schedules and dispose portable key state.

> [!WARNING]
> `Aes256` is a deterministic, single-block ECB primitive with no authentication. Do not compose it into multi-block ECB encryption.

```csharp
using var aes = new Aes256(key);
Vector128<byte> ciphertext = aes.Encrypt(plaintext);
Vector128<byte> recovered = aes.Decrypt(ciphertext);

Vector128<byte> portableCiphertext = aes.EncryptWithFramework(plaintext);
if (Aes256.IsSupported)
{
    Vector128<byte> intrinsicPlaintext = aes.DecryptRuntimeIntrinsics(portableCiphertext);
}
```

### Hashing (`HashExtensions`)

High-performance hashing extensions supporting modern and legacy algorithms.
*   **Blake3**: Default modern cryptographic hash (fast and secure).
*   **XxHash3**: Non-cryptographic hashing for performance-critical scenarios (IDs, Cache keys).
*   **Security**: Keyed hashing support (`HashWithKey`) for integrity protection.
*   **Streams**: Stream overloads hash files and large payloads without first materializing them as `BinaryData`; prefer these overloads for file and upload hashing.

### JSON & Document Utilities

*   **Safe JSON Merge Patch**: `JsonMergePatch.SafeApplyMergePatch(target, patch)` implements RFC 7396 processing semantics without mutating either input. Semantic no-ops reuse the target; changed object targets are cloned once and merged without repeated subtree cloning. `MergeResult` is a readonly record struct, `Json` is nullable for a root-level JSON `null` result, and `Changed` reports only actual document changes.
*   **In-Place JSON Merge Patch**: `JsonMergePatch.ApplyMergePatchInPlace(ref target, patch)` and `ApplyMergePatchInPlace(targetObject, patchObject)` perform full RFC 7396 merge patch operations directly in-place, preserving existing nested object references and updating the `ref target` reference if the root type changes.
*   **Resource Safety**: All merge methods validate the complete patch depth before applying changes. The repository unit suite includes every RFC 7396 Appendix A example.
*   **Query String Serialization**: `QueryParameterSerializer` flattens complex nested objects/arrays into clean query strings for API clients.

### Serialization & Streams

*   **Unified Extensions**: `model.Serialize(method)` supports both JSON and Query String formats.
*   **Safe Stream Consumption**: `ToBinaryDataAsync` and `ToArrayAsync` extensions with `MaxSizeGuard` to prevent memory exhaustion from untrusted streams.

### Programmatic Validation

Extensions for programmatic validation using `System.ComponentModel.DataAnnotations`.
*   **Contextual**: Integrates with `DRN.Framework.SharedKernel.ValidationException` for standardized error reporting across layers.

### Entity Creation-Date Filters (`IEntityDateTimeUtils`)

`IEntityDateTimeUtils` filters `SourceKnownEntity.Id` by its 250ms Source-Known ID creation tick without requiring database timestamp columns. Each date boundary maps to minimum and maximum scalar `long` ID bounds for efficient query evaluation.

```csharp
public class OrderService(IEntityDateTimeUtils dateTimeUtils)
{
    public IQueryable<Order> GetOrdersInDateRange(IQueryable<Order> query, DateTimeOffset start, DateTimeOffset end)
    {
        return dateTimeUtils.CreatedBetween(query, start, end, inclusive: true);
    }
}
```

| Filter | Inclusive boundary | Exclusive boundary |
|---|---|---|
| `CreatedAfter` | `Id >= tick.Min` | `Id > tick.Max` |
| `CreatedBefore` | `Id <= tick.Max` | `Id < tick.Min` |
| `CreatedBetween` | `Id >= begin.Min && Id <= end.Max` | `Id > begin.Max && Id < end.Min` |
| `CreatedOutside` | `Id <= begin.Max \|\| Id >= end.Min` | `Id < begin.Min \|\| Id > end.Max` |

## Pagination

The framework provides `IPaginationUtils` for cursor-based pagination ordered by the temporal sequence of `SourceKnownEntityId`.

```csharp
public class OrderDto(Order order) : Dto(order)
{
    public bool Active { get; } = order.Active;
}

public class OrderService(IPaginationUtils pagination, OrderDbContext dbContext)
{
    public async Task<PaginationResultModel<OrderDto>> GetRecentOrdersAsync(PaginationRequest request)
    {
        var query = dbContext.Orders.Where(x => x.Active);
        var result = await pagination.GetResultAsync(query, request);
        return result.ToModel(order => new OrderDto(order));
    }
}
```

## Bit Packing

For scenarios requiring custom ID generation or compact binary data structures, use `NumberBuilder` and `NumberParser`. `NumberBuilder<TNumber>` is a `ref struct`; `NumberParser` is a value-type parser for low-allocation bit manipulation.

```csharp
// Use NumberBuilder to pack data into a long
var builder = NumberBuilder.GetLong();
builder.TryAddNibble(0x05);  // Add 4 bits
builder.TryAddUShort(65535); // Add 16 bits
long packedValue = builder.GetValue();

// Use NumberParser to unpack
var parser = NumberParser.Get(packedValue);
byte nibble = parser.ReadNibble();
ushort value = parser.ReadUShort();
```

```csharp
// Multi-format serialization
var json = model.Serialize(SerializationMethod.SystemTextJson);
var query = model.Serialize(SerializationMethod.QueryString);

// Data Integrity
var hash = data.Hash(HashAlgorithm.Blake3);
var fileHash = fileStream.Hash(HashAlgorithm.Sha256);

// Secure stream conversion
var bytes = await requestStream.ToBinaryDataAsync(maxSize: 1024 * 1024);
```

## Validators

Reusable validators live under `DRN.Framework.Utils.Validators`.

```csharp
using DRN.Framework.Utils.Validators;

var validation = await JpegValidator.ValidateAsync(requestStream, maxLength: 1024 * 1024);
if (!validation.IsValid)
{
    var message = validation.ErrorReason switch
    {
        JpegValidationErrorReason.MaxLengthExceeded => "Profile picture exceeds the maximum allowed size.",
        JpegValidationErrorReason.InvalidMaxLength => "Profile picture maximum size must be zero or greater.",
        _ => "Profile picture must be a valid JPEG image."
    };
    throw ExceptionFor.Validation(message);
}

var imageBytes = validation.ImageData;
```

`JpegValidator` performs structural JPEG checks for markers, segment bounds, frame metadata, scan metadata, scan data presence, and optional maximum byte length. `JpegValidationResult.ErrorReason` distinguishes `MaxLengthExceeded`, `InvalidMaxLength`, and `InvalidJpeg` failures. Use `ValidateAsync` when validating an upload stream and keeping the validated bytes for persistence.

## Diagnostics

### Development Status

Track database migration status and pending model changes in real-time during development.

```csharp
public class StartupService(DevelopmentStatus status, IScopedLog log)
{
    public void CheckStatus()
    {
        if (status.HasPendingChanges)
        {
            log.AddToActions("Warning: Pending database changes detected");
            foreach (var model in status.Models)
            {
                 model.LogChanges(log, "Development");
            }
        }
    }
}
```

## Time & Async

### High-Performance Time (`TimeStampManager`)

For systems requiring frequent timestamp lookups (like ID generation or rate limiting), `TimeStampManager` provides a cached UTC timestamp updated periodically (default 10ms) to reduce `DateTimeOffset.UtcNow` overhead.

```csharp
long precisionTicks = TimeStampManager.CurrentTimestamp(EpochTimeUtils.DefaultEpoch);
DateTimeOffset now = TimeStampManager.UtcNow; // Cached UTC time truncated to 250ms precision
```

### Async-Safe Timer (`RecurringAction`)

An atomic timer implementation that prevents overlapping executions if one cycle takes longer than the period.

```csharp
using var worker = new RecurringAction(async () => {
    await DoHeavyWork();
}, period: 1000, start: true);

worker.Stop();
worker.Start(); // Resume after stopping
```

`Stop()` prevents an active callback from rescheduling the timer after it completes. The callback itself is allowed to finish.

### ID Generation & Validation

**SourceKnownEntity ID's** provide reversible, type-safe, and integrity-checked identifiers.
> [!NOTE]
> ID generation is automatically handled by `DrnContext` when SourceKnownEntities are saved.

#### Generation Modes

The `Generate` method dispatches to secure or plain generation based on the `UseSecureSourceKnownIds` flag in `NexusAppSettings` (defaults to `true`). Explicit `GenerateSecure` and `GeneratePlain` methods are also available to bypass the flag.

| Method | Behavior |
|--------|----------|
| `Generate` | Dispatches to secure or plain based on `UseSecureSourceKnownIds` |
| `GenerateSecure` | AES-256-ECB encrypted — full 16-byte GUID is a ciphertext block |
| `GeneratePlain` | Plaintext with visible `8D8D` version/variant markers |
| `ToSecure` | Converts a plain ID to its secure form (idempotent) |
| `ToPlain` | Converts a secure ID to its plain form (idempotent) |

**Secure variant** encrypts the entire 16-byte GUID with `Aes256` as a pseudo-random permutation (PRP). For a single 128-bit block, ECB is mathematically identical to CBC with a zero IV — no nonce required, no nonce-reuse vulnerability. Key separation ensures BLAKE3 keyed MAC (integrity) and AES-256 (confidentiality) use cryptographically independent keys derived from the same decoded `NexusKey` material.

Generation uses the default `NexusKey`. Parse uses a default-first key-ring fallback, so IDs generated before key rotation can still be parsed while the previous key remains configured.

> [!NOTE]
> `SourceKnownEntityIdUtils` is a singleton and safely reuses each key-ring entry's `Aes256` instance across concurrent calls. It uses lock-free runtime intrinsics when available and automatically falls back to the lock-free .NET 10.0.10 framework provider, preserving the encrypted ID format on hosts without AES intrinsics. Reverify this framework-provider concurrency assumption when changing the target runtime.

```csharp
// Generate with flag-based dispatch (secure by default)
var entityId = sourceKnownEntityIdUtils.Generate<User>(id);

// Explicitly secure
var secureId = sourceKnownEntityIdUtils.GenerateSecure<User>(id);

// Explicitly plain (visible markers for debugging/development)
var plainId = sourceKnownEntityIdUtils.GeneratePlain<User>(id);

// Convert between secure and plain forms (idempotent)
var convertedSecureId = sourceKnownEntityIdUtils.ToSecure(plainEntityId);
var convertedPlainId = sourceKnownEntityIdUtils.ToPlain(secureEntityId);
```

#### Parse & Validation

`Parse` accepts secure and plaintext IDs and verifies their integrity.

> [!IMPORTANT]
> Add rate limiting to endpoints that accept `SourceKnownEntityId` from untrusted sources to prevent brute-force attacks.

Users can validate incoming IDs (e.g., from APIs) using multiple approaches depending on the context:

**1. Injectable Utility (Recommended for Service Layer)**
```csharp
var sourceKnownId = sourceKnownEntityIdUtils.Validate<User>(externalGuidId);
```

**2. SourceKnownRepository (Recommended for Data Access)**
```csharp
// Method on SourceKnownRepository<TEntity>
var sourceKnownId = userRepository.GetEntityId(externalGuidId); 
```

**3. SourceKnownEntity (Recommended for Domain Logic)**
```csharp
// Helper on SourceKnownEntity base class
var sourceKnownId = userInstance.GetEntityId<User>(externalGuidId);
```

#### GUID Byte Layout

The plaintext form of a `SourceKnownEntityId` (SKEID) packs identity, integrity, time-addressing, and UUID V8 compatibility (RFC 9562 §5.8) into a single 128-bit GUID. Secure IDs are opaque ciphertext and are not guaranteed to retain UUID version or variant bits.

| Byte(s) | Purpose |
|---------|---------|
| 0 | Epoch index (8 bits; current releases support epoch 0) |
| 1–4 | SKID upper half (32 bits, sign-toggled) |
| 5 | SKID low byte 0 (MSB of SKID lower half / timestamp LSB) |
| 6 | Version marker (`0x8D` — UUID V8, RFC 9562 §5.8) |
| 7 | Entity type (8 bits — up to 256 entity types) |
| 8 | Variant marker (`0x8D` — RFC 4122 compatible) |
| 9–11 | SKID low bytes (remaining 24 bits) |
| 12–15 | BLAKE3 keyed MAC (32 bits — integrity verification) |

#### Epoch & Time Addressing

SourceKnownEntityIds use epoch-based time addressing for monotonic ordering. Current releases support the first epoch, which starts on **2025-01-01** and spans approximately **68 years** ($2^{31}$ seconds total coverage, split across two halves).

| Property | Value |
|----------|-------|
| Epoch start | 2025-01-01 |
| Supported duration | ~68 years ($2^{31}$ seconds) |
| Supported epoch | 0 |

> [!NOTE]
> Current releases support epoch 0 only; generation rejects timestamps outside its supported range.

### Time

`TimeProvider` singleton is registered by default to `TimeProvider.System` for testable time entry. See [Time & Async](#time--async) for high-performance alternatives.

## Concurrency

### Lock-Free Atomic Utilities (`LockUtils`)

`LockUtils` provides static helpers for lock-free atomic operations built on `Interlocked`. Use these primitives to coordinate concurrent access without OS-level locks.

| Method | Purpose |
|--------|---------|
| `TryClaimLock(ref int)` | Atomically claims a lock (0 → 1). Returns `true` if successful. |
| `TryClaimScope(ref int)` | Returns a disposable `LockScope` that auto-releases on dispose. |
| `ReleaseLock(ref int)` | Unconditionally releases a lock (→ 0). |
| `TrySetIfEqual<T>(ref T?, T, T?)` | Atomic CAS for reference types; sets value if current is the same reference as comparand. |
| `TrySetIfNull<T>(ref T?, T)` | Sets value only if current is `null`. |
| `TrySetIfNotEqual<T>(ref T?, T, T?)` | Sets value only if current is **not** the same reference as comparand (retry loop). |
| `TrySetIfNotNull<T>(ref T?, T)` | Sets value only if current is **not** `null`. |

`TrySetIfNotEqual` and `TrySetIfNotNull` use bounded retries (`maxRetries`, default `100`). A `false` result can mean either that the comparison condition was not met or that retries were exhausted.

```csharp
// Disposable lock scope (preferred) — auto-releases on dispose
private int _lock;

using var scope = LockUtils.TryClaimScope(ref _lock);
if (scope.Acquired) { /* critical section */ }

// One-time initialization guard
private MyService? _instance;
var service = new MyService();
LockUtils.TrySetIfNull(ref _instance, service);
```

## Extensions

Comprehensive set of extensions for standard .NET types and reflection.

### Reflection & `MethodUtils`

High-performance reflection helpers with unified caching for generic and non-generic method discovery and execution.
*   **Invoke**: `instance.InvokeMethod("Name", args)` and `type.InvokeStaticMethod("Name", args)`.
*   **Generics**: `instance.InvokeMethod("Name", typeArgs, args)` and `type.InvokeStaticMethod("Name", typeArgs, args)`.
*   **Zero-Alloc Overloads**: Specialized 0, 1, 2, 3 argument and `Span<object?>` overloads eliminate `params` array allocation.
*   **Caching & Execution**: Unified `FindMethod` cache with `MethodCacheKey` and zero-allocation execution powered by runtime `MethodInvoker`.
*   **Uncached Discovery**: `type.FindMethodUncached(...)` for explicit cache-bypassing scenarios (e.g. one-off startup discovery).

### Service Collection

Advanced DI container manipulation for testing and modularity.
*   **Querying**: `sc.GetAllAssignableTo<TService>()` retrieves all descriptors matching a type.
*   **Replacement**: `ReplaceScoped`, `ReplaceSingleton`, and `ReplaceInstance` for mocking/overriding dependencies in integration tests.

### String & Binary Extensions

*   **Parsing**: `string.Parse<T>()` and `string.TryParse<T>(out result)` using the modern `IParsable<T>` interface.
*   **Binary**: `ToStream()` and `ToByteArray()` shortcuts with UTF8 default.
*   **FileSystem**: `GetLines()` for `IFileInfo` with efficient physical path reading.

Casing and safe path helpers live in `DRN.Framework.SharedKernel.Extensions`.

### Type & Assembly Extensions

*   **Discovery**: `assembly.GetSubTypes(typeof(T))` and `assembly.GetTypesAssignableTo(to)`.
*   **Instantiation**: `assembly.CreateSubTypes<T>()` automatically discovers and instantiates classes with parameterless constructors.
*   **Metadata**: `type.GetAssemblyName()` returns a clean assembly name.

### Flurl & HTTP Diagnostics

*   **Logging**: `PrepareScopeLogForFlurlExceptionAsync()` adds Flurl failure diagnostics to `IScopedLog`, and DRN Hosting applies it to unhandled Flurl exceptions. Captured request and response data is not automatically redacted; catch sensitive failures before they reach Hosting, or use the `Try*` converters and log only sanitized fields.
*   **Status Codes**: `GetGatewayStatusCode()` preserves `4xx`, `503`, and `504` statuses and maps other statuses to `502`.
*   **Testing**: `ClearFilteredSetups()` utility for complex test scenarios.

### Object & Dictionary Extensions

*   **Deep Discovery**: `instance.GetGroupedPropertiesOfSubtype(type)` recursively finds properties matching a base type across complex object graphs.
*   **Dictionary Utility**: Extensions for `IDictionary` to handle null-safe value retrieval and manipulation.
*   **Bit Manipulation**: `GetBitPositions()` for `long` values and bitmask generators for signed/unsigned lengths.

```csharp
// Discovery and Instantiation
var implementations = typeof(IMyInterface).Assembly.CreateSubTypes<IMyInterface>();

// Modern Parsing
int value = "123".Parse<int>();

// Binary shortcuts
using var body = "payload".ToStream();
```

---

## Suggested Consumer Global Usings

```csharp
global using DRN.Framework.SharedKernel;
global using DRN.Framework.SharedKernel.Extensions;
global using DRN.Framework.Utils.DependencyInjection;
```

---

## Related Packages

- [DRN.Framework.SharedKernel](https://www.nuget.org/packages/DRN.Framework.SharedKernel/) - Domain primitives and exceptions
- [DRN.Framework.EntityFramework](https://www.nuget.org/packages/DRN.Framework.EntityFramework/) - EF Core integration
- [DRN.Framework.Hosting](https://www.nuget.org/packages/DRN.Framework.Hosting/) - Web application hosting
- [DRN.Framework.Testing](https://www.nuget.org/packages/DRN.Framework.Testing/) - Testing utilities

For complete examples, see [Sample.Hosted](https://github.com/duranserkan/DRN-Project/tree/master/Sample.Hosted).

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/.agent/rules/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
