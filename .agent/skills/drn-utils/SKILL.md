---
name: drn-utils
description: DRN.Framework.Utils - Attribute-based dependency injection ([Scoped], [Singleton], [Transient]), IAppSettings configuration pattern, logging infrastructure, extension methods, and core utilities. Foundational for service registration, configuration management, and cross-cutting concerns. Keywords: dependency-injection, di, service-registration, configuration, appsettings, logging, extensions, attributes, scoped, singleton, transient, skills, drn, domain, design, overview, framework, shared, kernel, hosting, entity, testing
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
├── Http/                 # HTTP helpers
├── Ids/                  # ID generation
├── Numbers/              # Numeric utilities
├── Scope/                # ScopeContext
├── Time/                 # Time utilities
├── Entity/               # Entity utilities
├── Cancellation/         # Cancellation utilities
├── Models/               # Shared models
└── UtilsModule.cs        # Module registration
```

---

## Module Registration

```csharp
services.AddDrnUtils(); // Registers all attribute-based services
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

```csharp
public interface IMyService { }

[Scoped<IMyService>]
public class MyService : IMyService { }
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

### HasServiceCollectionModuleAttribute

For custom registration patterns (like DrnContext):

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

Bind configuration sections to classes:

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
| Env vars not binding | Wrong format | Use `__` for nested: `Section__Key` |
| Mounted settings not loading | Wrong path | Check `/appconfig/` or override via `IMountedSettingsConventionsOverride` |

---

## Scoped Logging (IScopedLog)

Request-scoped structured logging:

```csharp
public class MyService(IScopedLog scopedLog)
{
    public void DoWork()
    {
| `Add(key, value)` | Add structured log entry |
| `AddToActions(msg)` | Add entry to execution trail |
| `Measure(key)` | Capture execution duration |
| `AddException(ex)` | Logs exception with inner details |
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

## Extension Methods

### ServiceCollectionExtensions

```csharp
services.ReplaceInstance<T>(instance);
services.ReplaceTransient<TService, TImpl>();
services.ReplaceScoped<TService, TImpl>();
services.ReplaceSingleton<TService, TImpl>();
services.GetAllAssignableTo<TService>();
```

### StringExtensions

```csharp
"hello world".ToStream();       // Convert to stream
"camelCase".ToPascalCase();     // Convert to PascalCase
```

### TypeExtensions

```csharp
type.MakeGenericMethod(methodName, typeArgs);
```

### AssemblyExtensions

```csharp
assembly.GetTypesAssignableTo<TInterface>();
```

---

## ScopeContext (Ambient Context)

Access scoped data anywhere:

```csharp
ScopeContext.UserId;
ScopeContext.IsUserInRole("Admin");
ScopeContext.IsClaimFlagEnabled("FeatureX");
ScopeContext.Log.Add("Key", "Value");
ScopeContext.Settings;
ScopeContext.TraceId;
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

---

## Related Skills

- [drn-domain-design.md](../drn-domain-design/SKILL.md) - Domain & Repository patterns
- [overview-drn-framework.md](../overview-drn-framework/SKILL.md) - Framework overview
- [drn-sharedkernel.md](../drn-sharedkernel/SKILL.md) - Domain primitives
- [drn-hosting.md](../drn-hosting/SKILL.md) - Web hosting
- [drn-entityframework.md](../drn-entityframework/SKILL.md) - Database access
- [drn-testing.md](../drn-testing/SKILL.md) - Testing with contexts

---
