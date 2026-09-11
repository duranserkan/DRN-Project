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

## Overview

- Register public, concrete services with DI attributes and `AddServicesWithAttributes()`; bind settings with `[Config]` and `IAppSettings`.
- Collect operation data with `IScopedLog`, coordinate root and keyed cancellation, and access initialized request scopes through `ScopeContext`.
- Generate and validate source-known IDs, filter by creation time, and paginate by internal ID order.
- Encode, hash, encrypt, merge JSON, validate JPEGs, resolve app data paths, and pack bits with the data utilities.
- Use HTTP factories and response converters with explicit failure and disposal contracts; evaluate configured claims and MFA evidence with authentication helpers.

## Table of Contents

- [QuickStart: Beginner](#quickstart-beginner)
- [QuickStart: Advanced](#quickstart-advanced)
- [Setup](#setup)
- [Dependency Injection](#dependency-injection)
- [Configuration](#configuration)
- [Logging (IScopedLog)](#logging-iscopedlog)
- [HTTP Client Factories (IExternalRequest, IInternalRequest)](#http-client-factories-iexternalrequest-iinternalrequest)
- [Scope & Ambient Context (ScopeContext)](#scope--ambient-context-scopecontext)
- [TOTP Generation and Verification](#totp-generation-and-verification)
- [Data Utilities](#data-utilities)
- [Pagination](#pagination)
- [Bit Packing](#bit-packing)
- [Validators](#validators)
- [Diagnostics](#diagnostics)
- [Time & Async](#time--async)
- [ID Generation & Validation](#id-generation--validation)
- [Concurrency](#concurrency)
- [Extensions](#extensions)
- [Suggested Consumer Global Usings](#suggested-consumer-global-usings)
- [Related Packages](#related-packages)

---

## QuickStart: Beginner

This console example registers the calling assembly and resolves a scoped service. It uses Development settings for the example; see [Configuration](#configuration) for deployed applications.

```csharp
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton<IAppSettings>(_ => AppSettings.Development(new { NexusAppSettings = new { AppId = 0 } }));
services.AddServicesWithAttributes();

using var provider = services.BuildServiceProvider();
await provider.ValidateServicesAddedByAttributesAsync();
using var scope = provider.CreateScope();
Console.WriteLine(scope.ServiceProvider.GetRequiredService<IGreetingService>().Greet("World"));

public interface IGreetingService { string Greet(string name); }

[Scoped<IGreetingService>]
public class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello, {name}!";
}

```

In an MVC application, inject `IGreetingService` into the controller and call `Greet` from the action.

```csharp
using Microsoft.AspNetCore.Mvc;

public class HomeController(IGreetingService greetingService) : Controller
{
    public IActionResult Index() => Ok(greetingService.Greet("World"));
}
```

## QuickStart: Advanced

Add these types to the same assembly to combine configuration binding and scoped logging. The example checks a timeout and records an amount; it does not call a payment provider.

```csharp
using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;

[Config]
public class PaymentSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}

public interface IPaymentService { PaymentResult Process(decimal amount); }
public sealed record PaymentResult(bool Success);

[Scoped<IPaymentService>]
public class PaymentService(IAppSettings settings, IScopedLog log, PaymentSettings config) : IPaymentService
{
    public PaymentResult Process(decimal amount)
    {
        using var duration = log.Measure("PaymentProcessing");
        log.Add("Amount", amount);
        log.Add("Environment", settings.Environment.ToString());
        log.AddToActions("Processing payment");

        if (config.TimeoutSeconds < 10)
            throw ExceptionFor.Configuration("Timeout too short");

        return new PaymentResult(Success: true);
    }
}
```

`[Config]` binds the `PaymentSettings` section by class name. Supply that section through configuration or use the class defaults when it is absent. Inside an initialized DRN Hosting request scope, `ScopeContext.UserId` supplies the ambient user ID. Console and worker code should use injected services; see [Scope & Ambient Context](#scope--ambient-context-scopecontext).

---

## Setup

> [!NOTE]
> If you are using `DRN.Framework.Hosting` (inheriting from `DrnProgramBase`), this package is **automatically registered and validated**.

The package targets .NET 10. Add it to a consumer project with:

```bash
dotnet add package DRN.Framework.Utils
```

For manual registration in a console app or worker with an existing builder:

```csharp
using DRN.Framework.Utils;
using DRN.Framework.Utils.DependencyInjection;

// Registers Utils services, HybridCache and TimeProvider.
builder.Services.AddDrnUtils();
// Registers attributed services in the calling application assembly.
builder.Services.AddServicesWithAttributes();
```

`AddServicesWithAttributes()` also calls `AddDrnUtils()` when scanning a consumer assembly, so the first call is optional in that case. Manual applications must supply `IConfiguration` or `IAppSettings`. Neither registration call creates an ambient `ScopeContext`. See [UtilsModule.cs](UtilsModule.cs) and [registration extensions](DependencyInjection/ServiceCollectionExtensions.cs).

The sections below show consumer snippets. Names such as `Order`, `OrderDbContext`, `StatusResponse`, `SomeAsyncOp`, `model`, and stream variables belong to the consuming application. Import the namespaces of the linked APIs; the snippets do not define an entire application.

### HybridCache Registration

`AddDrnUtils()` registers Microsoft's `HybridCache` with default in-memory caching. To configure distributed caching (e.g., Redis), add your `IDistributedCache` registration before calling `AddDrnUtils()`:

```csharp
// Requires the Microsoft.Extensions.Caching.StackExchangeRedis package.
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

`AddServicesWithAttributes()` scans the calling assembly by default; pass an `Assembly` argument to scan another assembly. Lifetime discovery includes visible, concrete classes. Attribute definitions are in [LifetimeAttribute.cs](DependencyInjection/Attributes/LifetimeAttribute.cs).

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

A class can carry multiple registration attributes. Discovery enumerates all of them, deduplicates equal attributes across the assembly, and retains each distinct module's service descriptors for startup validation. Use `GetModuleAttributes(Type)` for plural discovery; `GetModuleAttribute(Type)` requires exactly one attribute.

**Example**: `DrnContext<T>` (in `DRN.Framework.EntityFramework`) is decorated with `[DrnContextServiceRegistration]`, which:
1.  Registers the DbContext.
2.  Runs startup migration handling; Development auto-migration occurs when `DrnDevelopmentSettings:AutoMigrateDevelopment` is enabled (default: `true`).

```csharp
using DRN.Framework.EntityFramework.Context;
using Microsoft.EntityFrameworkCore;

public class MyDbContext : DrnContext<MyDbContext>
{
    public MyDbContext() : base(null) { }
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }
}
```

The parameterless constructor supports design-time creation; the options constructor supports DI. See [DrnContext.cs](../DRN.Framework.EntityFramework/Context/DrnContext.cs).

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
    public string ApiKey { get; set; } = string.Empty;
}

[Config] // Binds to "FeatureFlags" section (class name)
public class FeatureFlags { public bool Preview { get; set; } }

[ConfigRoot] // Binds to root configuration
public class RootSettings { public string Environment { get; set; } = string.Empty; }
```

`[Config]` defaults to annotation validation, binding non-public properties, and rejecting unknown configuration keys. `[ConfigRoot]` binds the root and disables unknown-key rejection. Startup annotation validation runs through `ValidateServicesAddedByAttributesAsync()`.

### Configuration Sources

DRN Hosting's `AddDrnSettings` loads configuration in this order, with later sources taking precedence. `AddDrnUtils()` alone does not load these sources:
1.  `appsettings.json`
2.  `appsettings.{Environment}.json`
3.  User Secrets when the application assembly is available
4.  Environment variables (`ASPNETCORE_`, `DOTNET_`, then unprefixed)
5.  **Mounted Settings**:
    -   `/appconfig/key-per-file-settings/*`
    -   Every file in `/appconfig/json-settings/`, loaded as JSON
6.  Command-line arguments

`Environment` is required and must be `Development`, `Staging`, or `Production`. DRN validates the value used to select `appsettings.{Environment}.json`; define it in `appsettings.json`, environment variables, mounted settings, or command-line arguments, and do not override it in environment-specific JSON or user secrets.

Override the mount directory by registering `IMountedSettingsConventionsOverride`. See [Hosting configuration extensions](../DRN.Framework.Hosting/Extensions/ConfigurationExtensions.cs).

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

This JSON shows Development defaults. Replace `SeedKey` with private key material for Staging or Production.

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

#### Seed keys and derived settings

`DrnAppFeatures.SeedKey` feeds [AppSecuritySettings](Settings/AppSecuritySettings.cs). BLAKE3 derive-key mode uses a distinct DRN Framework context string for each output:

| Output | Representation | Use |
|---|---|---|
| `AppHashKey` | Base64Url-encoded 32 bytes | Private hashing key |
| `AppEncryptionKey` | Base64Url-encoded 32 bytes | Private encryption key |
| `AppKey` | 8 characters | Public application discriminator |
| `AppSeed` | Signed 64-bit value | Seed-dependent operations |

Changing `SeedKey` changes app-specific names, rate-limit keyed hashes, Development default Nexus key material, and seed-dependent operations. `AppSettings` enforces these constraints at startup:
- The built-in default `SeedKey` (`DrnAppFeatures.DefaultSeedKey`) is only permitted in the `Development` environment and is rejected in `Staging`, `Production`, or `NotDefined`.
- The sample `SeedKey` (`DrnAppFeatures.SampleSeedKey`) is only permitted during test execution (`TestEnvironment.DrnTestContextEnabled == true`) and is rejected in all non-test application runs.

#### Settings reference

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

`NexusAppSettings` configures Nexus routing, generator instances, secure/plain IDs, and the key ring used by `SourceKnownEntityIdUtils`. Entity ID generation derives `AppId` from `[EntityType<TApp>]` metadata. Configured `NexusAppSettings.AppId` controls the host's client routing partition and host domain partition alignment in Entity Framework.

`NexusAppSettings:AppId` must be explicitly configured in every environment, including tests and `AppSettings.Development(...)`. Missing, null, empty and whitespace values fail configuration validation; explicit `0` is valid. The supported range remains 0 through 127.

The following example illustrates the key format. Supply private key material in deployed applications.

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

Invalid user-provided keys are not hashed, stretched, truncated, repaired, or treated as another format. Validation rejects malformed encodings, empty keys, wrong decoded lengths, and raw values that are not exactly 32 UTF-8 bytes. Exception messages avoid including the secret key value. `NexusKey.SampleKeyMaterial` is forbidden in all environments, including tests. See [NexusAppSettings.cs](Settings/NexusAppSettings.cs).

When Development has no default Nexus key, `AppSettings` derives deterministic 32-byte material from `AppSecuritySettings`. It stores the result in memory as `Base64UrlEncoded`, then applies the same BLAKE3 MAC/encryption key separation used for configured keys. This fallback is not random and is unavailable outside Development.

## Logging (`IScopedLog`)

`IScopedLog` aggregates structured operational data, metrics, checkpoints, and exceptions for a logical scope. In DRN Hosting request scopes, Hosting enriches and emits that aggregate as a single log entry.

### Core Features

*   **Contextual**: Every `ScopedLog` has a stable `CorrelationId` and captures an active W3C `TraceId` when available. Hosting adds request and user context.
*   **Aggregation**: Groups all actions, metrics, and exceptions into a single structured log entry.
*   **Performance Tracking**: Built-in measurement for code block durations and execution counts.
*   **Exception Recording**: `AddException` records exception details without changing control flow; callers remain responsible for recovery, rethrowing, and excluding sensitive data.

### Scope events

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

### OpenTelemetry correlation

`TraceId` captures `Activity.Current.TraceId` at scope construction when a W3C activity exists. Otherwise it is `null` and omitted from `GetLogs()`. `CorrelationId` is always generated for the scope. HTTP `TraceIdentifier` stays separate. These values remain stable; creating a log does not start or export a trace.

With the OpenTelemetry logging provider configured, native log `TraceId`, `SpanId`, and `TraceFlags` come from the activity active when the log is emitted. Emit within that activity for native correlation. The scoped snapshot does not populate native trace fields or restore ended activities. See [OpenTelemetry log correlation](https://opentelemetry.io/docs/languages/dotnet/logs/correlation/).

Use module-owned catalogs such as `OrderLogEvents` with stable IDs and names. IDs are unique within a module, not across all libraries. Filter dedicated events by logger category and ID. Scope events share this model through composition, not inheritance.

### Operation data

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

These factories wrap [Flurl](https://flurl.dev/) with framework JSON conventions and exact HTTP version requests. They do not configure retries or circuit breakers. See [FlurlRequestFactory.cs](Http/FlurlRequestFactory.cs).

### External Requests

Use `IExternalRequest` for standard external API calls. It pre-configures `DefaultJsonSerializer` and enforces HTTP version policies.

```csharp
public class PaymentService(IExternalRequest request)
{
    public async Task Process()
    {
        // Requests exactly HTTP/1.1.
        var response = await request.For("https://api.example.com", HttpVersion.Version11)
            .AppendPathSegment("v1/charges")
            .PostJsonAsync(new { Amount = 1000 })
            .FromJsonAsync<ExternalApiResponse>();
    }
}
```

### Internal Requests (Service Mesh)

`IInternalRequest` builds service URLs from a host name and the configured `InternalRequestProtocol` and `InternalRequestHttpVersion`. Overloads accept explicit `secure` and HTTP version values. In a Linkerd service mesh, HTTP can be used with mesh-managed mTLS. The factory selects the configured scheme; it does not discover infrastructure or negotiate a protocol switch.

#### Recommended Pattern: Request Wrappers

Instead of using `IInternalRequest` directly in business logic, wrap it in a typed request factory for better maintainability and configuration encapsulation.

```csharp
// 1. Typed internal request factory
public interface INexusRequest { IFlurlRequest For(string path); }

[Singleton<INexusRequest>]
public class NexusRequest(IInternalRequest request, IAppSettings settings) : INexusRequest
{
    private readonly string _nexusAddress = settings.NexusAppSettings.NexusAddress;
    public IFlurlRequest For(string path) => request.For(_nexusAddress).AppendPathSegment(path);
}

// 2. Client Usage
public class NexusClient(INexusRequest request)
{
    public async Task<HttpResponse<string>> GetStatusAsync() =>
        await request.For("status").GetAsync().ToStringAsync();
}
```

### Primary Handler Injection

`InternalRequest` and `ExternalRequest` accept an optional `HttpMessageHandler?` via constructor dependency injection. When an `HttpMessageHandler` is registered in DI (such as `ApplicationContextRouterHandler` during integration testing), `FlurlClient` instances use the injected handler for in-memory routing and interception without modifying global static Flurl state.

### Response conversion and failures

Buffered response converters (`ToStringAsync`, `ToBytesAsync`, and `FromJsonAsync`) capture `HttpStatus` and `Payload`, then dispose the response even if reading or deserialization fails.
Call converters as extension methods on `Task<IFlurlResponse>` or `IFlurlResponse`; `HttpResponse` models the converted result and no longer exposes static conversion entry points.

`HttpResponse.StatusClass` classifies the status as `Informational`, `Success`, `Redirection`, `ClientError`, `ServerError`, or `Unknown`; `IsSuccessStatusCode` is true only for `2xx`. Flurl normally follows redirects, so a `3xx` snapshot represents a redirect that remained final. Use `AllowAnyHttpStatus()` when the throwing converters should return non-success responses for direct inspection.

Use the `TryToStringAsync`, `TryToBytesAsync`, or `TryFromJsonAsync<T>` counterparts when transport, timeout, response-read, or deserialization failures must be inspected without catching exceptions:

```csharp
var result = await request.For("status").GetAsync().TryFromJsonAsync<StatusResponse>();
if (result.Failure is { } failure)
{
    // Apply failure policy here; Payload may be unavailable.
    log.Add("HttpFailureKind", failure.Kind.ToString()); // Injected IScopedLog.
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

### Streaming ownership

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

### Claim configuration and identity boundaries

`AuthenticationClaimConfig` is the shared claim contract. Hosting registers the result of `DrnProgramBase.ConfigureAuthenticationClaims()` once; ordinary Identity applications need no override. Standalone helpers use `AuthenticationClaimConfig.Default`. `Subject`, `Name`, `Email`, and `Roles` each expose a canonical `Type` and immutable `Aliases`; `Mfa` identifies one exact completed-MFA type/value.

| Mapping | Default canonical type | Explicit aliases |
| --- | --- | --- |
| Subject | `ClaimTypes.NameIdentifier` | `sub` |
| Name | `ClaimTypes.Name` | `name` |
| Email | `ClaimTypes.Email` | `email` |
| Roles | `ClaimTypes.Role` | `roles` |
| Mfa | `amr=mfa` | None |

`Subject = new("uid")` replaces the entire mapping, accepting only `uid`. Add aliases explicitly, for example `new("uid", "external_id")`. Scalar aliases must agree; roles combine only selected types. Subject agreement checks include case variants of configured types and aliases; unconfigured standard subject claims are ignored. `ScopedUser.Id` is null for missing or conflicting primary account evidence. Generic claim lookup remains case-insensitive; mapped security decisions use exact types.

Scoped name/email use the primary identity and return null for conflicting selected values or issuers. `IsInRole` checks all selected roles from authenticated identities across issuers. Generic claim groups retain their issuer filters.

Canonical types govern issuance and native `NameClaimType`/`RoleClaimType`; aliases are additional DRN inputs and do not rewrite claims or alter native authorization. Hosting configures Identity's claim options so its factory emits matching claims and metadata directly. Future authentication integrations must produce the same contract, mapping aliases into canonical claims only when needed, rejecting ambiguous evidence and excluding unselected case variants that native lookups could accept. Only validated identities belong in the application principal. Preserve claim provenance and identity boundaries; never infer MFA from `otp`, arbitrary `acr`, or a claim's name. See [Hosting integration](../DRN.Framework.Hosting/README.md#renewal-and-assurance).

### MFA completion and assurance

Scoped users, `MfaFor`, `MfaPrincipal`, and Hosting authorization share the same config without requiring Identity services. Setup/pending credentials cannot prove MFA; multiple authenticated identities must agree on subject and issuer.

The default subject mapping retains single-identity subjectless completion and same-object proof compatibility, including equivalent copies of the default mapping. Custom subject mappings, stronger assurance, Identity operations, and renewal require account evidence. Evaluate the final authorized `User` for account-security decisions.

For stronger opt-in checks, `MfaPrincipal.IsRecent(principal, config, trustedIssuer, maximumAge, utcNow, authenticationTimeClaimType)` and `IsPhishingResistant(principal, config, trustedIssuer, assuranceClaim)` require the completed marker and additional evidence on the same authenticated identity, from the specified issuer. All authenticated identities must have an unambiguous matching subject and issuer. Setup/pending credentials, missing subjects and untrusted evidence fail closed. `IsCompleted` and the default Hosting `Mfa` policy retain their current semantics.

`IsRecent` defaults to `auth_time`, accepts integer Unix seconds, rejects future/malformed/conflicting timestamps, and includes the exact maximum-age boundary. Pass the current time from `TimeProvider.GetUtcNow()`; a negative maximum age is invalid configuration. Authentication recency is not necessarily MFA recency: use a provider-guaranteed verified-MFA timestamp claim when that is the requirement. Renewal preserves existing `auth_time`; these helpers do not issue claims.

`IsPhishingResistant` requires an explicit `MfaClaimConfig` for an assurance marker distinct from the completed marker. Configure it only for an issuer/value that guarantees phishing-resistant authentication; generic `amr=mfa` and passkey labels do not automatically establish this. Provider validation/mapping and preservation of additional assurance claims remain application responsibilities. Missing assurance after renewal returns false.

The implementation is in [MfaPrincipal.cs](Auth/MFA/MfaPrincipal.cs); claim mappings are defined in [AuthenticationClaimConfig.cs](Auth/AuthenticationClaimConfig.cs).

### Ambient application data

`ScopeData` is separate caller-owned ambient storage and is not automatically copied into `IScopedLog`. Use `SetFlag` and typed `SetParameter` values for validated application data.

```csharp
var currentUserId = ScopeContext.UserId;
var traceId = ScopeContext.TraceId;
var settings = ScopeContext.Settings; // Static IAppSettings access
var logger = ScopeContext.Log; // Static IScopedLog access

var isAdmin = ScopeContext.IsUserInRole("Admin");

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

`Temp` and `Data` expose `Path`, `DirectoryExists`, and `Status`. The status describes resolution time; it is not a live filesystem check. `GetPath` rejects empty or invalid roots and paths escaping the root. It does not create the requested child directory. See [AppDataPathResult.cs](Data/App/AppDataPathResult.cs) and [App Data Settings](#app-data-settings).

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

A live instance supports concurrent calls. Intrinsic operations read pre-expanded round keys without locks or per-call allocation. The [source remarks](Data/Encryption/Aes256.cs) record portable-provider concurrency verification against .NET 10.0.10: each operation creates its own cipher state without changing the configured key. This is version-specific evidence and must be rechecked when changing the runtime. Dispose the instance after all callers finish to clear intrinsic schedules and dispose portable key state.

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

### Authenticated Encryption (`AesGcmEncryptorBase`)

For payload encryption with authentication, derive from `AesGcmEncryptorBase` and provide a stable, application-specific context. `IAppSecuritySettings.CreateAesGcm(context)` derives a dedicated 32-byte key from `AppEncryptionKey` using BLAKE3 and creates a cipher with 16-byte tags. Intermediate key buffers are cleared.

```csharp
using DRN.Framework.Utils.Data.Encryption;
using DRN.Framework.Utils.Settings;

public sealed class ExportEncryptor(IAppSecuritySettings settings) : AesGcmEncryptorBase(settings)
{
    protected override string Context => "ExampleApp ExportPayload v1";
}
```

With injected `IAppSecuritySettings securitySettings` and payload bytes:

```csharp
using var encryptor = new ExportEncryptor(securitySettings);
var encrypted = encryptor.Encrypt(payload);
var recovered = encryptor.Decrypt(encrypted.Nonce, encrypted.Ciphertext, encrypted.Tag);
```

`Encrypt` generates a fresh random 12-byte nonce and returns `AesGcmEncryptedData(Ciphertext, Nonce, Tag)`. Keep all three values for decryption. Span overloads accept caller-owned buffers; use 12 bytes for the nonce, 16 for the tag, and the payload length for ciphertext/plaintext. Authentication failures throw. Keep the derivation context stable for existing data and dispose the encryptor after use. See [AesGcmEncryptorBase.cs](Data/Encryption/AesGcmEncryptorBase.cs).

### Hashing (`HashExtensions`)

Hash extensions support cryptographic and non-cryptographic algorithms.
*   **Blake3**: Default cryptographic hash.
*   **XxHash3**: Non-cryptographic hashing for lookup and cache keys; not an integrity check against attackers.
*   **Security**: Keyed hashing support (`HashWithKey`) for integrity protection.
*   **Streams**: Stream overloads hash files and large payloads without first materializing them as `BinaryData`; prefer these overloads for file and upload hashing.

```csharp
var hash = data.Hash(HashAlgorithm.Blake3);
var fileHash = fileStream.Hash(HashAlgorithm.Sha256);
```

### JSON & Document Utilities

*   **Safe JSON Merge Patch**: `JsonMergePatch.SafeApplyMergePatch(target, patch)` implements RFC 7396 processing semantics without mutating either input. Semantic no-ops reuse the target; changed object targets are cloned once and merged without repeated subtree cloning. `MergeResult` is a readonly record struct, `Json` is nullable for a root-level JSON `null` result, and `Changed` reports only actual document changes.
*   **In-Place JSON Merge Patch**: `JsonMergePatch.ApplyMergePatchInPlace(ref target, patch)` and `ApplyMergePatchInPlace(targetObject, patchObject)` perform full RFC 7396 merge patch operations directly in-place, preserving existing nested object references and updating the `ref target` reference if the root type changes.
*   **Resource Safety**: All merge methods validate the complete patch depth before applying changes. The repository unit suite includes every RFC 7396 Appendix A example.
*   **Query String Serialization**: `QueryParameterSerializer` flattens nested objects and arrays into query strings for API clients.

Merge patch object properties set to `null` remove those properties; arrays and other non-object patches replace the target. The default maximum patch depth is 64 and must be positive. The query serializer has a separate default depth limit of 10. See [JsonMergePatch.cs](Data/Json/JsonMergePatch.cs) and [QueryParameterSerializer.cs](Data/Serialization/QueryParameterSerializer.cs).

### Serialization & Streams

*   **Unified Extensions**: `model.Serialize(method)` supports both JSON and Query String formats.
*   **Safe Stream Consumption**: `ToBinaryDataAsync` and `ToArrayAsync` extensions with `MaxSizeGuard` to prevent memory exhaustion from untrusted streams.

```csharp
var json = model.Serialize(SerializationMethod.SystemTextJson);
var query = model.Serialize(SerializationMethod.QueryString);
var bytes = await requestStream.ToBinaryDataAsync(maxSize: 1024 * 1024);
```

The stream helpers default to a 10 MiB limit and throw `ValidationException` when it is exceeded. Seekable streams are read from the current position and restored afterward; non-seekable streams are consumed. The helpers leave the input stream open and accept cancellation. See [StreamExtensions.cs](Data/Serialization/StreamExtensions.cs).

`Serialize(SystemTextJson)` uses `JsonSerializer.Serialize(model)` with its default options. `Deserialize<T>` supports JSON only; query-string deserialization is not provided.

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

`CreatedBetween` and `CreatedOutside` normalize reversed endpoints. When both endpoints fall in the same tick, inclusive `Between` selects that entire tick and exclusive `Between` selects nothing. Inclusive `Outside` selects all rows; exclusive `Outside` excludes that tick. `Apply(query, EntityCreatedFilter)` dispatches by filter type and requires an end date for `Between` and `Outside`. See [EntityDateTimeUtils.cs](Entity/EntityDateTimeUtils.cs).

## Pagination

`IPaginationUtils` paginates by the internal `SourceKnownEntity.Id` (`long`). External GUID cursors are parsed and checked for validity, then converted to internal IDs for filtering. Secure GUID ciphertext is not used as the sort key.

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

The query must support EF Core asynchronous execution. `GetResultAsync` accepts an optional cancellation token, handles next/previous/refresh navigation, and fetches one extra item for page metadata. Explicit page jumps use `Skip`; ordinary cursor navigation uses ID comparisons. A total-count query runs only when `UpdateTotalCount` is requested. See [PaginationUtils.cs](Entity/PaginationUtils.cs).

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

Use the same `NumberBuildDirection` and residue-bit settings for packing and parsing. The defaults are most-significant-first with 32 residue bits for signed `long`. `TryAdd*` returns `false` when there is insufficient remaining capacity; values are masked to the requested bit width. Validate value ranges before packing when truncation is unacceptable. See [NumberBuilder.cs](Numbers/NumberBuilder.cs) and [NumberParser.cs](Numbers/NumberParser.cs).

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

The default limit is 10 MiB. `Validate` and `ValidateAsync` return bytes only through the validated end-of-image marker, excluding trailing data. Span-based `IsValid` also requires any trailing bytes to be permitted padding. Structural validation does not decode image pixels. See [JpegValidator.cs](Validators/JpegValidator.cs).

## Diagnostics

### Development Status

`DevelopmentStatus` collects database model and migration information during context startup. `HasPendingChanges` reports pending model changes; inspect each model's `Flags.HasPendingMigrations` for unapplied migrations. It is not a continuous database monitor. See [DevelopmentStatus.cs](Models/DevelopmentStatus.cs).

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

`TimeStampManager` provides cached UTC with 250ms precision for repeated timestamp reads.

```csharp
long precisionTicks = TimeStampManager.CurrentTimestamp();
DateTimeOffset now = TimeStampManager.UtcNow; // Cached UTC time truncated to 250ms precision
```

Backward clock drift below five seconds freezes the cached timestamp until the clock catches up. Drift of at least five seconds requests application shutdown and causes timestamp reads to throw `ClockDriftException`. See [TimeStampManager.cs](Time/TimeStampManager.cs).

`CurrentTimestamp()` applies the [generation-time policy](#trusted-minimum-utc). `UtcNow` is a general cached read and does not enforce the generation floor.

### Recurring Timer & Dedicated Thread (`RecurringAction`)

`RecurringAction` prevents overlapping synchronous `Action` callbacks. Its `period` is a delay in milliseconds after a callback finishes, not a fixed interval between start times. Use `RecurringActionAsync` for asynchronous callbacks; do not pass an `async void` callback to `RecurringAction`.

Supplying `threadName` selects a dedicated background thread, with `ThreadPriority.Highest` as the default priority. Dedicated threads require a positive `period`; timer mode accepts zero. Invalid periods throw `ArgumentOutOfRangeException` during construction, including with `start: false`.

```csharp
// ThreadPool timer (default)
using var worker = new RecurringAction(SyncWork, period: 1000, start: true);

// Dedicated background thread
using var clockWorker = new RecurringAction(
    SyncWork, period: 10, threadName: "MyService.DedicatedWorker");

worker.Stop();
worker.Start(); // Resume after stopping
```

`Stop()` prevents an active callback from rescheduling after it completes. The callback itself is allowed to finish.

In dedicated-thread mode, restarting during an active callback preserves the full `period` delay after that callback finishes. After restarting a stopped worker, subsequent callbacks remain separated by the configured delay.

Subscribe to `OnActionFailed` to observe callback exceptions. `Dispose()` stops scheduling without waiting for an active callback. `Start()` throws after disposal.

### Non-Overlapping Async Timer (`RecurringActionAsync`)

`RecurringActionAsync` awaits asynchronous callbacks without overlap and supports cancellation and asynchronous disposal:

```csharp
await using var backgroundWorker = new RecurringActionAsync(
    async ct => {
        await PollExternalServiceAsync(ct);
    },
    period: TimeSpan.FromSeconds(30),
    executionTimeout: TimeSpan.FromSeconds(10));

backgroundWorker.Stop();
backgroundWorker.Start(); // Resume execution
```

`Stop()` requests cancellation; restarted execution waits for the previous callback to finish. `DisposeAsync()` cancels and waits for outstanding callbacks, including stopped runs. Use `await using` to await completion when leaving the scope.

Timeouts require a `Func<CancellationToken, Task>` callback. Pass the token to cancellable operations: a callback that ignores it can block later iterations and asynchronous disposal. `OnActionFailed` reports callback errors, or `TimeoutException` when the callback throws `OperationCanceledException` after its timeout.

Periods truncate to 1–4,294,967,294 whole milliseconds. Timeouts must be positive and truncate to at most the same upper limit; positive sub-millisecond timeouts become zero milliseconds. Invalid bounds throw `ArgumentOutOfRangeException` during construction, even with `start: false`. Tokenless `Func<Task>` constructors accept only the callback, period, and optional `start`; they execute without a timeout.

### Time

`AddDrnUtils()` uses `TryAddSingleton` to register `TimeProvider.System`. A previously registered `TimeProvider` is retained, allowing callers to supply testable time.

## ID Generation & Validation

### Trusted minimum UTC

New IDs use a process-wide epoch and minimum generation time:

| Setting | Default | Purpose |
|---|---|---|
| `SourceKnownIdSettings:MinimumUtc` | `2026-09-09T00:00:00Z` | Earliest permitted generated timestamp; an override may be earlier or later. |
| `SourceKnownIdSettings:DefaultEpoch` | `2025-01-01T00:00:00Z` | Origin used by generation, decoding, and date filters. Supplying it requires an explicit `MinimumUtc` in the same configuration. |

Both settings require ISO 8601 UTC ending in `Z` or `+00:00`, with seconds and up to seven fractional digits. For example:

```json
{
  "SourceKnownIdSettings": {
    "DefaultEpoch": "2026-01-01T00:00:00Z",
    "MinimumUtc": "2026-09-09T00:00:00Z"
  }
}
```

Configure before startup, generation, parsing, date-filter construction, or reading the configured epoch. These operations freeze both values; later conflicting settings fail, even after a failed generation-time check. Identical settings or omitted overrides retain the current policy. Non-hosted callers can use `SourceKnownGenerationTime.Initialize(minimumUtc, defaultEpoch)` before first use.

Keep the epoch unchanged across every service and restart using a dataset. IDs do not store their origin; changing it changes the interpretation of existing timestamps.

The minimum rounds upward to a 250ms boundary relative to the epoch. Hosts validate before program/actions constructors and hooks, including read-only and temporary hosts; non-hosted generation validates on first use. Invalid configuration, a time below the rounded minimum, or a time outside the supported epoch fails. A failed time check can be retried when time catches up.

Only encoded epoch `0`, including both halves, is supported. Historical parsing and GUID reconstruction from existing numeric IDs use the configured epoch without enforcing the generation floor. The floor constrains generated timestamps; [clock-drift protection](#high-performance-time-timestampmanager) still applies. See the [SharedKernel baseline](../DRN.Framework.SharedKernel/README.md#source-baseline-contract) for maintenance and cross-restart limits.

Source-known IDs separate the internal `long` ID from the external `SourceKnownEntityId` GUID. The external form adds entity metadata and a keyed integrity check; it can be plain or encrypted.
> [!NOTE]
> ID generation is automatically handled by `DrnContext` when SourceKnownEntities are saved.

### Generation Modes

The `Generate` method dispatches to secure or plain generation based on the `UseSecureSourceKnownIds` flag in `NexusAppSettings` (defaults to `true`). Explicit `GenerateSecure` and `GeneratePlain` methods are also available to bypass the flag.

| Method | Behavior |
|--------|----------|
| `Generate` | Dispatches to secure or plain based on `UseSecureSourceKnownIds` |
| `GenerateSecure` | AES-256-ECB encryption of the full 16-byte GUID block |
| `GeneratePlain` | Plaintext with visible `8D8D` version/variant markers |
| `ToSecure` | Converts a plain ID to its secure form (idempotent) |
| `ToPlain` | Converts a secure ID to its plain form (idempotent) |

The secure variant encrypts the entire 16-byte GUID with `Aes256` as a pseudo-random permutation (PRP). For this single block, ECB is equivalent to CBC with a zero IV and uses no nonce. It is deterministic: equal blocks under the same key produce equal ciphertext. Integrity comes from the separate 32-bit BLAKE3 keyed MAC, not from AES. BLAKE3 derives distinct MAC and encryption keys from the decoded `NexusKey` material.

Generation uses the default `NexusKey`. Parse uses a default-first key-ring fallback, so IDs generated before key rotation can still be parsed while the previous key remains configured.

> [!NOTE]
> `SourceKnownEntityIdUtils` is a singleton and reuses each key-ring entry's `Aes256` instance. Intrinsic and portable paths preserve the same encrypted ID format. See [AES-256 single-block encryption](#aes-256-single-block-encryption-aes256) for concurrency, runtime-verification and disposal requirements.

```csharp
// Generate with flag-based dispatch (secure by default)
var entityId = sourceKnownEntityIdUtils.Generate<User>(id);

// Explicitly secure
var secureId = sourceKnownEntityIdUtils.GenerateSecure<User>(id);

// Explicitly plain (visible markers for debugging/development)
var plainId = sourceKnownEntityIdUtils.GeneratePlain<User>(id);

// Convert between secure and plain forms (idempotent)
var convertedSecureId = sourceKnownEntityIdUtils.ToSecure(plainId);
var convertedPlainId = sourceKnownEntityIdUtils.ToPlain(secureId);
```

Generate the internal ID first, or use the parameterless external-ID overload:

```csharp
long internalId = sourceKnownIdUtils.Next<User>();
var externalId = sourceKnownEntityIdUtils.Generate<User>(internalId);
var anotherId = sourceKnownEntityIdUtils.Generate<User>();
```

`User` must derive from `SourceKnownEntity` and carry the required entity/app metadata. `Next<TEntity>()` derives the app partition from that metadata and uses the configured instance ID. Explicit `Next`/`Generate` overloads accept app and instance IDs. `GeneratePlain<TEntity>()` and `GenerateSecure<TEntity>()` also generate a new internal ID when called without arguments. See [SourceKnownIdUtils.cs](Ids/SourceKnownIdUtils.cs) and [SourceKnownEntityIdUtils.cs](Ids/SourceKnownEntityIdUtils.cs).

### Parse & Validation

`Parse` accepts secure and plaintext IDs and verifies their integrity.

`Parse(Guid)` returns a result with `Valid == false` for an invalid ID. `Validate<TEntity>` throws when integrity, entity type, or application partition does not match. A valid ID does not grant access to an entity; authorization remains the application's responsibility.

> [!IMPORTANT]
> Add rate limiting to endpoints that accept `SourceKnownEntityId` from untrusted sources to prevent brute-force attacks.

Users can validate incoming IDs (e.g., from APIs) using multiple approaches depending on the context:

**1. Injectable Utility (Recommended for Service Layer)**
```csharp
var sourceKnownId = sourceKnownEntityIdUtils.Validate<User>(externalGuidId);
```

`Validate<TEntity>` uses the entity's code-declared `(EntityType, AppId)`, independently of configuration. `Validate(id, entityType)` uses `NexusAppSettings.AppId`. Invalid ID integrity, entity type or encoded AppId throws `ValidationException`.

Without an entity type parameter, override the configured partition using an `IAppId` type or an explicit `EntityTypeId`:

```csharp
ids.Validate<Order>(orderId);                         // Order's declared identity
ids.Validate(orderId, entityType);                    // configured partition
ids.Validate<StockItem>(stockItemId);                 // StockItem's declared identity
ids.Validate<InventoryApp>(stockItemId, entityType);   // InventoryApp : IAppId
ids.Validate(stockItemId, new EntityTypeId(entityType, InventoryApp.AppId));
```

Nullable inputs return null. `Parse` and SharedKernel validation do not enforce the service's configured partition. Authorization remains separate.

**2. SourceKnownRepository (Recommended for Data Access)**

Repositories validate the target entity's declared `(EntityType, AppId)`, including registered secondary partitions.

```csharp
// Method on SourceKnownRepository<TEntity>
var sourceKnownId = userRepository.GetEntityId(externalGuidId); 
```

**3. SourceKnownEntity (Recommended for Domain Logic)**

Use entity metadata or `new EntityTypeId(entityType, expectedAppId)` to validate both identity components. `ValidateId()` and the domain GUID helper's boolean validation option check integrity only.

```csharp
// Helper on SourceKnownEntity base class
var sourceKnownId = userInstance.GetEntityId<User>(externalGuidId);
```

### GUID Byte Layout

The plaintext form of a `SourceKnownEntityId` (SKEID) packs identity, integrity, time-addressing, and UUID V8 compatibility (RFC 9562 §5.8) into a single 128-bit GUID. Secure IDs are opaque ciphertext and are not guaranteed to retain UUID version or variant bits.

| Byte(s) | Purpose |
|---------|---------|
| 0 | Epoch index (8 bits; current releases support epoch 0) |
| 1–4 | SKID upper half (32 bits, sign-toggled) |
| 5 | SKID low byte 0 (MSB of SKID lower half / timestamp LSB) |
| 6 | Version marker (`0x8D`, UUID V8, RFC 9562 §5.8) |
| 7 | Entity type (8 bits, up to 256 entity types) |
| 8 | Variant marker (`0x8D`, RFC 4122 compatible) |
| 9–11 | SKID low bytes (remaining 24 bits) |
| 12–15 | BLAKE3 keyed MAC (32 bits, integrity verification) |

### Epoch & Time Addressing

SourceKnownEntityIds use epoch-based time addressing for monotonic ordering. Current releases support the first epoch, which starts on **2025-01-01** and spans approximately **68 years** ($2^{31}$ seconds total coverage, split across two halves).

| Property | Value |
|----------|-------|
| Epoch start | 2025-01-01 |
| Supported duration | ~68 years ($2^{31}$ seconds) |
| Supported epoch | 0 |

> [!NOTE]
> Current releases support epoch 0 only; generation rejects timestamps outside its supported range.

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
using DRN.Framework.Utils.Concurrency;

public sealed class WorkGate
{
    private int _lock;
    private object? _instance;

    public bool TryRun(Action work)
    {
        using var scope = LockUtils.TryClaimScope(ref _lock);
        if (!scope.Acquired) return false;
        work();
        return true;
    }

    public bool TryInitialize(object instance) =>
        LockUtils.TrySetIfNull(ref _instance, instance);
}
```

## Extensions

Extensions cover .NET types, DI descriptors, HTTP diagnostics, and dynamic method discovery.

### Reflection & `MethodUtils`

`MethodUtils` caches generic and non-generic method discovery and execution. Prefer `[UnsafeAccessor]` when the target type is known and accessible at compile time; use reflection for dynamically discovered types.
*   **Invoke**: `instance.InvokeMethod("Name", args)` and `type.InvokeStaticMethod("Name", args)`.
*   **Generics**: `instance.InvokeMethod("Name", typeArgs, args)` and `type.InvokeStaticMethod("Name", typeArgs, args)`.
*   **Argument Overloads**: Specialized 0, 1, 2, 3 argument and `Span<object?>` overloads avoid a `params` argument array. Boxing and other caller allocations can still occur.
*   **Caching & Execution**: `FindMethod` caches discovery through `MethodCacheKey`; invocation uses runtime `MethodInvoker`.
*   **Uncached Discovery**: `type.FindMethodUncached(...)` for explicit cache-bypassing scenarios (e.g. one-off startup discovery).

Pass generic type arguments as an explicit `Type[]`, such as `instance.InvokeMethod("Name", [typeof(string)])`. A single `typeof(string)` argument selects an ordinary method accepting `Type`. The former generic/fast aliases were removed; use `FindMethod`, `FindMethodUncached`, `InvokeMethod`, and `InvokeStaticMethod`. For an already resolved method, use `MethodInvoker.Create(methodInfo)` or a compiled delegate. See [MethodUtils.cs](Extensions/MethodUtils.cs).

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

*   **Deep Discovery**: `instance.GetGroupedPropertiesOfSubtype(type)` groups properties whose declared type is a subtype of the requested type. It excludes exact-type matches and does not traverse collection elements. The default recursion limit is 5; property getters are evaluated and can throw.
*   **Dictionary Utility**: `GetAndCastValueOrDefault` returns a typed value or the supplied fallback; `UpdateIf` changes an existing entry only when its predicate passes. The dictionary itself must be non-null.
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
