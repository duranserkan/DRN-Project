---
name: drn-utils
description: DRN.Framework.Utils - Attribute-based dependency injection ([Scoped], [Singleton], [Transient], [HostedService], [Config]), IAppSettings configuration pattern, logging infrastructure, extension methods, and core utilities. Foundational for service registration, configuration management, and cross-cutting concerns. Keywords: dependency-injection, di, service-registration, configuration, appsettings, logging, extensions, attributes, scoped, singleton, transient, hostedservice, backgroundservice, config, configroot, skills, drn, domain, design, overview, framework, shared, kernel, hosting, entity, testing
---

# DRN.Framework.Utils

> Core utilities package providing attribute-based DI, configuration, logging, and extensions.

## When to Apply
- Setting up dependency injection with attributes
- Accessing configuration via IAppSettings
- Using or extending logging (IScopedLog)
- Working with DRN extension methods
- Understanding service registration patterns

---

## Package Purpose

Utils is the **core infrastructure** package. Most other DRN packages depend on it.

---

## Directory Structure

```
DRN.Framework.Utils/
├── DependencyInjection/  # Lifetime attributes, assembly scanning
├── Settings/             # IAppSettings, conventions
├── Configurations/       # Configuration sources
├── Logging/              # IScopedLog, ScopeLog
├── Extensions/           # ServiceCollection, String, Type extensions
├── Auth/                 # Authentication helpers
├── Data/                 # Data utilities
├── Http/                 # HTTP helpers (IExternalRequest, IInternalRequest)
├── Ids/                  # ID generation
├── Numbers/              # NumberBuilder, NumberParser (bit packing)
├── Scope/                # ScopeContext
├── Time/                 # TimeStampManager, RecurringAction
├── Entity/               # Entity utilities, IPaginationUtils
├── Cancellation/         # ICancellationUtils
├── Models/               # Shared models
└── UtilsModule.cs        # Module registration
```

---

## Module Registration

```csharp
// Registers attribute-based services, HybridCache, and TimeProvider
services.AddDrnUtils();
```

### HybridCache Registration

`AddDrnUtils()` registers Microsoft's `HybridCache` with default in-memory caching. To configure distributed caching (e.g., Redis), add your `IDistributedCache` registration before calling `AddDrnUtils()`:

```csharp
builder.Services.AddStackExchangeRedisCache(options => 
{
    options.Configuration = "localhost:6379";
});
builder.Services.AddDrnUtils(); // HybridCache will use the distributed cache if available
```

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
> All lifetime attributes accept an optional `tryAdd` parameter (default: `true`). When `true`, `TryAdd` is used so existing registrations are not overwritten. Set to `false` to allow multiple implementations of the same service type.

```csharp
public interface IMyService { }

[Scoped<IMyService>]
public class MyService : IMyService { }
```

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
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

### Registration

```csharp
// In module:
public static IServiceCollection AddMyServices(this IServiceCollection sc)
{
    sc.AddServicesWithAttributes(); // Scans calling assembly
    return sc;
}

// Validation:
serviceProvider.ValidateServicesAddedByAttributesAsync(); // Validate registered services health
```

### Module Registration & Startup Actions

Services can require complex registration logic or post-startup actions. Attributes inheriting from `ServiceRegistrationAttribute` handle this.

**Example**: `DrnContext<T>` is decorated with `[DrnContextServiceRegistration]`, which:
1. Registers the DbContext.
2. Automatically triggers EF Core Migrations when the application starts in Development environments (via `PostStartupValidationAsync`).

```csharp
[HasDrnContextServiceCollectionModule]
public class MyDbContext : DrnContext<MyDbContext> { }
```

---

## Configuration (IAppSettings)

```csharp
public interface IAppSettings
{
    AppEnvironment Environment { get; }
    IConfiguration Configuration { get; }
    DrnAppFeatures Features { get; }
    DrnLocalizationSettings Localization { get; }
    DrnDevelopmentSettings DevelopmentSettings { get; }
    NexusAppSettings NexusAppSettings { get; }
    bool IsDevEnvironment { get; }
    string AppKey { get; }
    string ApplicationName { get; }
    
    bool TryGetConnectionString(string name, out string connectionString);
    string GetRequiredConnectionString(string name);
    bool TryGetSection(string key, out IConfigurationSection section);
    IConfigurationSection GetRequiredSection(string key);
    T? GetValue<T>(string key);
    T? GetValue<T>(string key, T defaultValue);
    T? Get<T>(string key, bool errorOnUnknownConfiguration = false, bool bindNonPublicProperties = true);
    ConfigurationDebugView GetDebugView();
}
```

### Config Attribute

Bind configuration sections to classes (registered as **Singletons**):

```csharp
[Config("MySection")]      // Binds appsettings:MySection
public class MySettings { }

[Config]                   // Uses class name as section
public class FeatureFlags { }

[ConfigRoot]               // Binds to root
public class RootSettings { }
```

---

## Configuration Sources

Configuration applied in order (later overrides earlier):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User secrets (development)
4. Environment variables (`ASPNETCORE_`, `DOTNET_`, all)
5. Mounted settings:
   - `/appconfig/json-settings/*.json`
   - `/appconfig/key-per-file-settings/*`
6. Command line arguments

### MountedSettingsConventions

```csharp
MountedSettingsConventions.DefaultMountDirectory; // "/appconfig"

// Override mount directory:
public class MyOverride : IMountedSettingsConventionsOverride
{
    public string? MountedSettingsDirectory => "/custom/config";
}
```

### IAppSettings Troubleshooting

| Symptom | Cause | Solution |
|---------|-------|----------|
| `ConfigurationException` | Missing required key | Add to `appsettings.json` |
| `GetRequiredConnectionString` throws | Key not found | Check `ConnectionStrings` section |
| `IsDevEnvironment` always false | `ASPNETCORE_ENVIRONMENT` not set | Set env var or use `launchSettings.json` |
| Env vars not binding | Wrong format | Use `__` for nested: `Section__Key` |
| Mounted settings not loading | Wrong path | Check `/appconfig/` or override via `IMountedSettingsConventionsOverride` |

---

## Scoped Logging (IScopedLog)

Request-scoped structured logging. Aggregates operational data, metrics, and checkpoints during the request lifecycle, flushing them as a single entry.

### API

| Method | Purpose |
|--------|---------|
| `Add(key, value)` | Add structured log entry |
| `AddIfNotNullOrEmpty(key, value)` | Conditional structured log entry |
| `AddToActions(msg)` | Add entry to execution trail |
| `AddProperties(key, object)` | Flatten and add complex objects |
| `Measure(key)` | Capture execution duration and count |
| `AddException(ex, msg)` | Logs exception with inner details |
| `Increase(key, by)` | Atomically increment a counter |
| `IncreaseTimeSpentOn(key, by)` | Accumulate time for a metric |

```csharp
public interface IScopedLog
{
    TimeSpan ScopeDuration { get; }
    IReadOnlyDictionary<string, object> Logs { get; }
    bool HasException { get; }
    bool HasWarning { get; }

    IScopedLog Add(string key, object value);
    void AddToActions(string action);
    void AddException(Exception exception, string? message = null);
    ScopeDuration Measure(string key);
    long Increase(string key, long by = 1, string prefix = "Stats");
}
```

**Automatically captured**:
- TraceId
- Request (path, method, host, IP)
- Response (status, length)
- Exceptions with inner details
- Scope duration

---

## HTTP Client Factories (`IExternalRequest`, `IInternalRequest`)

Wrappers around [Flurl](https://flurl.dev/) for resilient HTTP clients with standardized JSON conventions.

### External Requests

Use `IExternalRequest` for standard external API calls. Pre-configures `DefaultJsonSerializer` and enforces HTTP version policies.

```csharp
public class PaymentService(IExternalRequest request)
{
    public async Task Process()
    {
        var response = await request.For("https://api.example.com", HttpVersion.Version11)
            .AppendPathSegment("v1/charges")
            .PostJsonAsync(new { Amount = 1000 })
            .ToJsonAsync<ExternalApiResponse>();
    }
}
```

### Internal Requests (Service Mesh)

Use `IInternalRequest` for Service-to-Service communication in Kubernetes. Designed to work with Linkerd, supporting automatic protocol switching (HTTP/HTTPS).

**Recommended Pattern: Request Wrappers**:
```csharp
public interface INexusRequest { IFlurlRequest For(string path); }

[Singleton<INexusRequest>]
public class NexusRequest(IInternalRequest request, IAppSettings settings) : INexusRequest
{
    private readonly string _nexusAddress = settings.NexusAppSettings.NexusAddress;
    public IFlurlRequest For(string path) => request.For(_nexusAddress).AppendPathSegment(path);
}
```

---

## Scoped Cancellation (`ICancellationUtils`)

Manage request-scoped cancellation tokens. Supports merging tokens from multiple sources (e.g., `HttpContext.RequestAborted` and application-level timeouts).

```csharp
public class MyScopedService(ICancellationUtils cancellation)
{
    public async Task DoWorkAsync(CancellationToken externalToken)
    {
        cancellation.Merge(externalToken); // Merges with the scoped token
        await SomeAsyncOp(cancellation.Token);
        
        if (cancellation.IsCancellationRequested)
            return;
    }
}
```

---

## Data Utilities

### Encodings (`EncodingExtensions`)
Unified API for binary-to-text encodings and model serialization-encoding.
- **Encodings**: Base64, Base64Url (safe for URLs), Hex, and Utf8.
- **Integrated**: `model.Encode(ByteEncoding.Hex)` and `hexString.Decode<TModel>()`.

### Hashing (`HashExtensions`)
High-performance hashing extensions supporting modern and legacy algorithms.
- **Blake3**: Default modern cryptographic hash (fast and secure).
- **XxHash3**: Non-cryptographic hashing for performance-critical scenarios (IDs, cache keys).
- **Security**: Keyed hashing support (`HashWithKey`) for integrity protection.

### JSON & Document Utilities
- **JSON Merge Patch**: `JsonMergePatch.SafeApplyMergePatch` follows RFC 7386 with recursion depth protection.
- **Query String Serialization**: `QueryParameterSerializer` flattens complex nested objects/arrays into clean query strings.

### Serialization & Streams
- **Unified Extensions**: `model.Serialize(method)` supports JSON and Query String formats.
- **Safe Stream Consumption**: `ToBinaryDataAsync` and `ToArrayAsync` with `MaxSizeGuard` to prevent memory exhaustion from untrusted streams.

### Programmatic Validation
Extensions for programmatic validation using `System.ComponentModel.DataAnnotations`.
- **Contextual**: Integrates with `DRN.Framework.SharedKernel.ValidationException` for standardized error reporting.

---

## Pagination (`IPaginationUtils`)

High-performance, monotonic cursor-based pagination leveraging temporal sequence of `SourceKnownEntityId`.

```csharp
public class OrderService(IPaginationUtils pagination)
{
    public async Task<PaginationResult<Order>> GetRecentOrdersAsync(PaginationRequest request)
    {
        var query = dbContext.Orders.Where(x => x.Active);
        return await pagination.GetResultAsync(query, request);
    }
}
```

---

## Bit Packing (`NumberBuilder` / `NumberParser`)

Zero-allocation bit manipulation for custom ID generation or compact binary data structures.

```csharp
// Pack data into a long
var builder = NumberBuilder.GetLong();
builder.TryAddNibble(0x05);  // Add 4 bits
builder.TryAddUShort(65535); // Add 16 bits
long packedValue = builder.GetValue();

// Unpack
var parser = NumberParser.Get(packedValue);
byte nibble = parser.ReadNibble();
ushort value = parser.ReadUShort();
```

---

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
                 model.LogChanges(log, "Development");
        }
    }
}
```

---

## Time & Async

### High-Performance Time (`TimeStampManager`)

Cached UTC timestamp updated periodically (default 10ms) to reduce `DateTimeOffset.UtcNow` overhead for frequent timestamp lookups.

```csharp
long seconds = TimeStampManager.CurrentTimestamp(EpochTimeUtils.DefaultEpoch);
DateTimeOffset now = TimeStampManager.UtcNow; // Cached precision up to the second
```

### Async-Safe Timer (`RecurringAction`)

Lock-free, atomic timer preventing overlapping executions.

```csharp
var worker = new RecurringAction(async () => {
    await DoHeavyWork();
}, period: 1000, start: true);
worker.Stop();
```

### Time
`TimeProvider` singleton is registered by default to `TimeProvider.System` for testable time entry.

---

## ScopeContext (Ambient Context)

Access scoped data anywhere. Simplifies cross-cutting concerns like auditing, multi-tenancy, and security by avoiding deep parameter passing, especially in Razor Pages (`.cshtml`).

```csharp
ScopeContext.UserId;
ScopeContext.TraceId;
ScopeContext.Authenticated;
ScopeContext.Settings;      // Static IAppSettings access
ScopeContext.Log;           // Static IScopedLog access
ScopeContext.IsUserInRole("Admin");
ScopeContext.IsClaimFlagEnabled("FeatureX");
```

---

## ID Generation

Source-known ID utilities:

```csharp
// 1. Internal Long ID (Database PK)
public interface ISourceKnownIdUtils
{
    long Next<TEntity>() where TEntity : class;
}

// 2. External Guid ID (Public API)
public interface ISourceKnownEntityIdUtils
{
    // Generate new from internal ID
    SourceKnownEntityId Generate<TEntity>(long id);
    SourceKnownEntityId Generate(long id, byte entityType);

    // Validate incoming GUID
    SourceKnownEntityId Parse(Guid entityId); // Check format only
    SourceKnownEntityId Validate(Guid entityId, byte entityType); // Check format + Type byte
    SourceKnownEntityId Validate<TEntity>(Guid entityId);
}

// Usage
var internalId = sourceKnownIdUtils.Next<User>();
var externalId = sourceKnownEntityIdUtils.Generate<User>(internalId);
```

> [!NOTE]
> ID generation is automatically handled by `DrnContext` when SourceKnownEntities are saved.

---

## Extension Methods

### Reflection & `MethodUtils`
Highly optimized reflection helpers with built-in caching.
- **Invoke**: `instance.InvokeMethod("Name", args)` and `type.InvokeStaticMethod("Name", args)`.
- **Generics**: `instance.InvokeGenericMethod("Name", typeArgs, args)` with static and uncached variations.
- **Caching**: Uses `ConcurrentDictionary` and `record struct` keys for zero-allocation lookups.

### ServiceCollectionExtensions

```csharp
services.ReplaceInstance<T>(instance);
services.ReplaceTransient<TService, TImpl>();
services.ReplaceScoped<TService, TImpl>();
services.ReplaceSingleton<TService, TImpl>();
services.GetAllAssignableTo<TService>();
```

### String & Binary Extensions

```csharp
"hello world".ToStream();            // Convert to stream
"camelCase".ToPascalCase();          // Convert to PascalCase
"MyPropertyName".ToSnakeCase();      // my_property_name
"MyProperty".ToCamelCase();          // myProperty
int value = "123".Parse<int>();      // Modern IParsable<T>
bool ok = "abc".TryParse<int>(out _); // Safe attempt
```

### Type & Assembly Extensions

```csharp
assembly.GetTypesAssignableTo<TInterface>();
assembly.GetSubTypes(typeof(T));
assembly.CreateSubTypes<T>(); // Discover and instantiate with parameterless ctors
type.GetAssemblyName();
```

### Flurl & HTTP Diagnostics
- **Logging**: `PrepareScopeLogForFlurlExceptionAsync()` captures exhaustive request/response metadata into `IScopedLog`.
- **Status Codes**: `GetGatewayStatusCode()` maps API errors to standard gateway codes (502, 503, 504).

### Object & Dictionary Extensions
- **Deep Discovery**: `instance.GetGroupedPropertiesOfSubtype(type)` recursively finds matching properties.
- **Bit Manipulation**: `GetBitPositions()` for `long` values and bitmask generators.

---

## Global Usings

```csharp
global using DRN.Framework.SharedKernel;
global using DRN.Framework.Utils.DependencyInjection;
```

---

## Related Skills

- [drn-domain-design.md](../drn-domain-design/SKILL.md) - Domain & Repository patterns
- [overview-drn-framework.md](../overview-drn-framework/SKILL.md) - Framework overview
- [drn-sharedkernel.md](../drn-sharedkernel/SKILL.md) - Domain primitives
- [drn-hosting.md](../drn-hosting/SKILL.md) - Web hosting
- [drn-entityframework.md](../drn-entityframework/SKILL.md) - Database access
- [drn-testing.md](../drn-testing/SKILL.md) - Testing with contexts

---
