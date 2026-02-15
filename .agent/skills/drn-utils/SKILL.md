---
name: drn-utils
description: DRN.Framework.Utils - Attribute-based dependency injection ([Scoped], [Singleton], [Transient], [HostedService], [Config]), IAppSettings configuration pattern, logging infrastructure, extension methods, and core utilities. Foundational for service registration, configuration management, and cross-cutting concerns. Keywords: dependency-injection, di, service-registration, configuration, appsettings, logging, scoped-log, attributes, scoped, singleton, transient, config, extensions, http-client
last-updated: 2026-02-15
difficulty: intermediate
---

# DRN.Framework.Utils

> Core utilities: attribute-based DI, configuration, logging, and extensions.

## When to Apply
- Setting up dependency injection with attributes
- Accessing configuration via IAppSettings
- Using or extending logging (IScopedLog)
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

### Module Registration & Startup Actions

Attributes inheriting from `ServiceRegistrationAttribute` handle complex registration and post-startup actions. Example: `DrnContext<T>` uses `[DrnContextServiceRegistration]` to auto-register the DbContext and trigger migrations in Development.

---

## Configuration (IAppSettings)

```csharp
public interface IAppSettings
{
    AppEnvironment Environment { get; }
    IConfiguration Configuration { get; }
    DrnAppFeatures Features { get; }
    bool IsDevEnvironment { get; }
    string AppKey { get; }
    string ApplicationName { get; }
    
    bool TryGetConnectionString(string name, out string connectionString);
    string GetRequiredConnectionString(string name);
    T? GetValue<T>(string key);
    T? Get<T>(string key, bool errorOnUnknownConfiguration = false);
    ConfigurationDebugView GetDebugView();
}
```

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

1. `appsettings.json` → 2. `appsettings.{Environment}.json` → 3. User secrets → 4. Environment variables → 5. Mounted settings (`/appconfig/`) → 6. Command line arguments

Override mount directory via `IMountedSettingsConventionsOverride`.

| Symptom | Solution |
|---------|----------|
| `ConfigurationException` | Add missing key to `appsettings.json` |
| Env vars not binding | Use `__` for nested: `Section__Key` |
| Mounted settings not loading | Check `/appconfig/` or override via `IMountedSettingsConventionsOverride` |

---

## Scoped Logging (IScopedLog)

Request-scoped structured logging. Aggregates data, metrics, and checkpoints, flushing as a single entry.

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
    .ToJsonAsync<ExternalApiResponse>();

// Internal Service Mesh (Kubernetes/Linkerd)
public interface INexusRequest { IFlurlRequest For(string path); }

[Singleton<INexusRequest>]
public class NexusRequest(IInternalRequest request, IAppSettings settings) : INexusRequest
{
    public IFlurlRequest For(string path) => request.For(settings.NexusAppSettings.NexusAddress)
        .AppendPathSegment(path);
}
```

---

## ScopeContext (Ambient Context)

Access scoped data anywhere without parameter passing:

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
```

> [!NOTE]
> ID generation is automatically handled by `DrnContext` when SourceKnownEntities are saved.

---

## Concurrency (`LockUtils`)

Lock-free atomic operations via `Interlocked`:

| Method | Purpose |
|--------|---------|
| `TryClaimLock(ref int)` | Atomically claim (0→1) |
| `TryClaimScope(ref int)` | Disposable auto-release scope |
| `ReleaseLock(ref int)` | Unconditionally release (→0) |
| `TrySetIfNull<T>(ref T?, T)` | CAS set-if-null |
| `TrySetIfEqual<T>(ref T?, T, T?)` | CAS compare-and-swap |
| `TrySetIfNotEqual<T>(ref T?, T, T?)` | Set if current ≠ comparand (retry loop) |
| `TrySetIfNotNull<T>(ref T?, T)` | Set if current is not null |

```csharp
private int _lock;
using var scope = LockUtils.TryClaimScope(ref _lock);
if (scope.Acquired) { /* critical section */ }
```

---

## Utilities Reference

| Area | Key Types | Purpose |
|------|-----------|---------| 
| **Data Encoding** | `EncodingExtensions` | Base64, Base64Url, Hex, Utf8 |
| **Hashing** | `HashExtensions` | Blake3 (crypto), XxHash3 (fast), keyed hashing |
| **JSON** | `JsonMergePatch` | RFC 7386 merge patch with depth protection |
| **Query Strings** | `QueryParameterSerializer` | Complex objects → query strings |
| **Streams** | `ToBinaryDataAsync` | Safe consumption with `MaxSizeGuard` |
| **Validation** | `ValidationExtensions` | DataAnnotations-based programmatic validation |
| **Pagination** | `IPaginationUtils` | Cursor-based via `SourceKnownEntityId` |
| **Cancellation** | `ICancellationUtils` | Merge tokens from multiple sources |
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

```csharp
// Cached UTC timestamp (10ms precision) — avoids DateTimeOffset.UtcNow overhead
long seconds = TimeStampManager.CurrentTimestamp(EpochTimeUtils.DefaultEpoch);
DateTimeOffset now = TimeStampManager.UtcNow;

// Lock-free async timer — prevents overlapping executions
var worker = new RecurringAction(async () => await DoWork(), period: 1000, start: true);
worker.Stop();
```

`TimeProvider` singleton registered to `TimeProvider.System` by default for testable time.

### Extension Methods

```csharp
// ServiceCollectionExtensions
services.ReplaceInstance<T>(instance);
services.ReplaceTransient<TService, TImpl>();
services.ReplaceScoped<TService, TImpl>();
services.ReplaceSingleton<TService, TImpl>();
services.GetAllAssignableTo<TService>();

// String & Binary
"hello world".ToStream();       "camelCase".ToPascalCase();
"MyProp".ToSnakeCase();         "MyProp".ToCamelCase();
int v = "123".Parse<int>();     bool ok = "abc".TryParse<int>(out _);

// Type & Assembly
assembly.GetTypesAssignableTo<TInterface>();
assembly.GetSubTypes(typeof(T));
assembly.CreateSubTypes<T>(); // Discover + instantiate parameterless ctors
type.GetAssemblyName();

// Reflection (cached invokers)
instance.InvokeMethod("Name", args);
type.InvokeStaticMethod("Name", args);
instance.InvokeGenericMethod("Name", typeArgs, args);

// Flurl & HTTP Diagnostics
await PrepareScopeLogForFlurlExceptionAsync(); // Captures request/response into IScopedLog
exception.GetGatewayStatusCode();              // Maps to 502/503/504

// Object & Dictionary
object.GetGroupedPropertiesOfSubtype<T>();      // Group properties by subtype
number.GetBitPositions();                       // Get set bit positions
```

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
