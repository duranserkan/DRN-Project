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

- **Attribute DI** - `[Scoped<T>]`, `[Singleton<T>]`, `[Transient<T>]` for zero-config service registration
- **Configuration** - `IAppSettings` with typed access, `[Config("Section")]` bindings
- **Scoped Logging** - `IScopedLog` aggregates structured logs per request
- **Scoped Cancellation** - Scoped `ICancellationUtils` for request lifecycle control
- **Monotonic Pagination** - Cursor-based pagination leveraging entity ID temporal ordering
- **Bit Packing** - High-performance `NumberBuilder` for custom data structures
- **Ambient Context** - `ScopeContext.UserId`, `ScopeContext.Settings` anywhere
- **Auto-Registration** - `AddServicesWithAttributes()` scans and registers all attributed services

## Table of Contents

- [QuickStart: Beginner](#quickstart-beginner)
- [QuickStart: Advanced](#quickstart-advanced)
- [Setup](#setup)
- [Dependency Injection](#dependency-injection)
- [Configuration](#configuration)
- [Logging (IScopedLog)](#logging-iscopedlog)
- [Scoped Cancellation](#scoped-cancellation)
- [HTTP Client Factories](#http-client-factories-iexternalrequest-iinternalrequest)
- [Scope & Ambient Context](#scope--ambient-context-scopecontext)
- [Data Utilities](#data-utilities)
- [Pagination](#pagination)
- [Bit Packing](#bit-packing)
- [Diagnostics](#diagnostics)
- [Time & Async](#time--async)
- [Extensions](#extensions)

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

// 2. Register all attributed services in Startup
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
[Config("PaymentSettings")]
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
        
        // Access ambient data anywhere
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

For manual installation (e.g. Console Apps, Workers):

```csharp
// Registers attributes, HybridCache, and TimeProvider
builder.Services.AddDrnUtils();
```

## Dependency Injection

### Attribute-Based Registration

Reduce wiring code by using attributes directly on your services. The registration method scans the calling assembly for these attributes.

| Attribute | Lifetime | Usage |
|-----------|----------|-------|
| `[Singleton<T>]` | Singleton | `[Singleton<IMyService>] public class MyService : IMyService` |
| `[Scoped<T>]` | Scoped | `[Scoped<IMyService>] public class MyService : IMyService` |
| `[Transient<T>]` | Transient | `[Transient<IMyService>] public class MyService : IMyService` |
| `[ScopedWithKey<T>]` | Scoped (Keyed) | `[ScopedWithKey<IMyService>("key")]` |

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
[Theory, DataInlineContext]
public void Validate_Dependencies(DrnTestContext context)
{
    context.ServiceCollection.AddServicesWithAttributes(); // Register local assembly
    context.ValidateServices(); // Verifies resolution of all registered descriptors
}
```

### Scoped Cancellation

Manage request-wide cancellation tokens efficiently using `ICancellationUtils`. It supports merging tokens from multiple sources (e.g., `HttpContext.RequestAborted` and internal timeouts).

```csharp
public class MyScopedService(ICancellationUtils cancellation)
{
    public async Task DoWorkAsync(CancellationToken externalToken)
    {
        // Automatically merges with the scoped token
        cancellation.Merge(externalToken);
        
        // Use the unified token
        await SomeAsyncOp(cancellation.Token);
        
        if (cancellation.IsCancellationRequested)
            return;
    }
}
```

### Module Registration & Startup Actions

Services can require complex registration logic or post-startup actions. Attributes inheriting from `ServiceRegistrationAttribute` handle this.

**Example**: `DrnContext<T>` (in `DRN.Framework.EntityFramework`) is decorated with `[DrnContextServiceRegistration]`, which:
1.  Registers the DbContext.
2.  **Automatically triggers EF Core Migrations** when the application starts in Development environments (via `PostStartupValidationAsync`).

```csharp
// The base class DrnContext handles the registration attributes.
// You just inherit from it, and your context is auto-registered with migration support.
public class MyDbContext : DrnContext<MyDbContext> { }
```

## Configuration

### IAppSettings

Access configuration safely with typed environments and utility methods.

```csharp
public class MyService(IAppSettings settings)
{
    public void DoWork()
    {
        if (settings.IsDevEnvironment) { ... }
        
        var conn = settings.GetRequiredConnectionString("Default");
        var value = settings.GetValue<int>("MySettings:Timeout", 30);
    }
}
```

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
1.  `appsettings.json` / `appsettings.{Environment}.json`
2.  Environment Variables
3.  **Mounted Settings**:
    -   `/appconfig/json-settings/*.json`
    -   `/appconfig/key-per-file-settings/*`

Override the mount directory by registering `IMountedSettingsConventionsOverride`.

## Logging (`IScopedLog`)

`IScopedLog` provides request-scoped structured logging. It aggregates logs, metrics, and actions throughout the request lifetime and flushes them as a single structured log entry at the end, making it ideal for high-traffic observability and performance monitoring.

### Core Features
*   **Contextual**: Automatically captures `TraceId`, `UserId`, `RequestPath`, and custom scope data.
*   **Aggregation**: Groups all actions, metrics, and exceptions into a single structured log entry.
*   **Performance Tracking**: Built-in measurement for code block durations and execution counts.
*   **Resilience**: Captures exceptions without interrupting the business flow unless explicitly thrown.

### API Usage

```csharp
public class OrderService(IScopedLog logger)
{
    public void ProcessOrder(int orderId)
    {
        // 1. Measure execution time and count
        // Automatically tracks duration and increments "Stats_ProcessOrder_Count"
        using var _ = logger.Measure("ProcessOrder"); 
        
        // 2. Add structured data (Key-Value)
        logger.Add("OrderId", orderId); 
        logger.AddIfNotNullOrEmpty("Referrer", "PartnerA");

        // 3. Track execution checkpoints
        logger.AddToActions("Validating order"); 
        
        try 
        {
            // ... logic ...
            // 4. Flatten and add complex objects
            logger.AddProperties("User", new { Name = "John", Role = "Admin" });
        }
        catch(Exception ex)
        {
            // 5. Log exception but keep the request contextual log intact
            logger.AddException(ex, "Failed to process order");
        }
    }
}
```

## HTTP Client Factories (`IExternalRequest`, `IInternalRequest`)

Lightweight wrappers around [Flurl](https://flurl.dev/) for consistent, resilient HTTP client configuration with built-in JSON convention support.

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
            .ToJsonAsync<ExternalApiResponse>();
    }
}
```

### Internal Requests (Service Mesh)
Use `IInternalRequest` for Service-to-Service communication in Kubernetes. It's designed to work with Linkerd/Istio, supporting automatic protocol switching (HTTP/HTTPS) based on infrastructure settings.

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

## Scope & Ambient Context (`ScopeContext`)

`ScopeContext` provides ambient (static) access to scoped information within a valid execution context (like an HTTP request). This is ideal for cross-cutting concerns like auditing, multi-tenancy, or security where deep parameter passing is undesirable.

*   **Contextual Identity**: Access `UserId`, `TraceId`, and `Authenticated` status anywhere.
*   **Static Accessors**: Provides direct access to `IAppSettings`, `IScopedLog`, and `IServiceProvider`.
*   **RBAC Helpers**: Built-in support for role and claim checks.

```csharp
var currentUserId = ScopeContext.UserId;
var traceId = ScopeContext.TraceId;
var settings = ScopeContext.Settings; // Static IAppSettings access
var logger = ScopeContext.Log; // Static IScopedLog access

if (ScopeContext.IsUserInRole("Admin")) { ... }
```

## Data Utilities

### Encodings (`EncodingExtensions`)
Unified API for binary-to-text encodings and model serialization-encoding.
*   **Encodings**: Base64, Base64Url (Safe for URLs), Hex, and Utf8.
*   **Integrated**: `model.Encode(ByteEncoding.Hex)` and `hexString.Decode<TModel>()`.

### Hashing (`HashExtensions`)
High-performance hashing extensions supporting modern and legacy algorithms.
*   **Blake3**: Default modern cryptographic hash (fast and secure).
*   **XxHash3**: Non-cryptographic hashing for performance-critical scenarios (IDs, Cache keys).
*   **Security**: Keyed hashing support (`HashWithKey`) for integrity protection.

### JSON & Document Utilities
*   **JSON Merge Patch**: `JsonMergePatch.SafeApplyMergePatch` follows RFC 7386 for partial updates with built-in recursion depth protection.
*   **Query String Serialization**: `QueryParameterSerializer` flattens complex nested objects/arrays into clean query strings for API clients.

### Serialization & Streams
*   **Unified Extensions**: `model.Serialize(method)` supports both JSON and Query String formats.
*   **Safe Stream Consumption**: `ToBinaryDataAsync` and `ToArrayAsync` extensions with `MaxSizeGuard` to prevent memory exhaustion from untrusted streams.

### programatic Validation
Extensions for programmatic validation using `System.ComponentModel.DataAnnotations`.
*   **Contextual**: Integrates with `DRN.Framework.SharedKernel.ValidationException` for standardized error reporting across layers.

## Pagination

The framework provides `IPaginationUtils` for high-performance, monotonic cursor-based pagination. It leverages the temporal sequence of `SourceKnownEntityId` to ensure stable results even as data is being added.

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

## Bit Packing

For scenarios requiring custom ID generation or compact binary data structures, use `NumberBuilder` and `NumberParser`. These ref-structs provide zero-allocation bit manipulation.

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

// Secure stream conversion
var bytes = await requestStream.ToBinaryDataAsync(maxSize: 1024 * 1024);
```

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
long seconds = TimeStampManager.CurrentTimestamp(EpochTimeUtils.DefaultEpoch);
DateTimeOffset now = TimeStampManager.UtcNow; // Cached precision up to the second
```

### Async-Safe Timer (`RecurringAction`)

A lock-free, atomic timer implementation that prevents overlapping executions if one cycle takes longer than the period.

```csharp
var worker = new RecurringAction(async () => {
    await DoHeavyWork();
}, period: 1000, start: true);

worker.Stop();
```

### ID Generation & Validation

**SourceKnownEntity ID's** provide reversible, type-safe, and integrity-checked identifiers.
> [!NOTE]
> ID generation is automatically handled by `DrnContext` when SourceKnownEntities are saved.

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

### Time
`TimeProvider` singleton is registered by default to `TimeProvider.System` for testable time entry. See [Time & Async](#time--async) for high-performance alternatives.

## Extensions

Comprehensive set of extensions for standard .NET types and reflection.

### Reflection & `MethodUtils`
Highly optimized reflection helpers with built-in caching for generic and non-generic method invocation.
*   **Invoke**: `instance.InvokeMethod("Name", args)` and `type.InvokeStaticMethod("Name", args)`.
*   **Generics**: `instance.InvokeGenericMethod("Name", typeArgs, args)` with static and uncached variations.
*   **Caching**: Uses internal `ConcurrentDictionary` and `record struct` keys for zero-allocation cache lookups.

### Service Collection
Advanced DI container manipulation for testing and modularity.
*   **Querying**: `sc.GetAllAssignableTo<TService>()` retrieves all descriptors matching a type.
*   **Replacement**: `ReplaceScoped`, `ReplaceSingleton`, and `ReplaceInstance` for mocking/overriding dependencies in integration tests.

### String & Binary Extensions
*   **Casing**: `ToSnakeCase`, `ToCamelCase`, and `ToPascalCase` for clean code-to-external system mapping.
*   **Parsing**: `string.Parse<T>()` and `string.TryParse<T>(out result)` using the modern `IParsable<T>` interface.
*   **Binary**: `ToStream()` and `ToByteArray()` shortcuts with UTF8 default.
*   **FileSystem**: `GetLines()` for `IFileInfo` with efficient physical path reading.

### Type & Assembly Extensions
*   **Discovery**: `assembly.GetSubTypes(typeof(T))` and `assembly.GetTypesAssignableTo(to)`.
*   **Instantiation**: `assembly.CreateSubTypes<T>()` automatically discovers and instantiates classes with parameterless constructors.
*   **Metadata**: `type.GetAssemblyName()` returns a clean assembly name.

### Flurl & HTTP Diagnostics
*   **Logging**: `PrepareScopeLogForFlurlExceptionAsync()` captures exhaustive request/response metadata from Flurl exceptions into `IScopedLog`.
*   **Status Codes**: `GetGatewayStatusCode()` maps API errors to standard gateway codes (502, 503, 504).
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

// Casing for APIs
var key = "MyPropertyName".ToSnakeCase(); // my_property_name
```

---
**Semper Progressivus: Always Progressive**