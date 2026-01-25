---
name: drn-utils
description: DRN.Framework.Utils - Attribute-based DI, IAppSettings, configuration, logging, extensions, and core utilities
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
serviceProvider.ValidateServicesAddedByAttributes();
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
    
    bool TryGetConnectionString(string name, out string connectionString);
    string GetRequiredConnectionString(string name);
    bool TryGetSection(string key, out IConfigurationSection section);
    IConfigurationSection GetRequiredSection(string key);
    T? GetValue<T>(string key);
    T? GetValue<T>(string key, T defaultValue);
    T? Get<T>(string key, Action<BinderOptions>? configureOptions = null);
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

---

## Scoped Logging (IScopedLog)

Request-scoped structured logging:

```csharp
public class MyService(IScopedLog scopedLog)
{
    public void DoWork()
    {
        scopedLog.Add("Operation", "DoWork");
        scopedLog.AddToActions("Started processing");
        
        using var duration = scopedLog.Measure("ProcessingTime");
        // work...
    }
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

- [overview-drn-framework.md](../overview-drn-framework/SKILL.md) - Framework overview
- [drn-sharedkernel.md](../drn-sharedkernel/SKILL.md) - Domain primitives
- [drn-hosting.md](../drn-hosting/SKILL.md) - Web hosting
- [drn-entityframework.md](../drn-entityframework/SKILL.md) - Database access
- [drn-testing.md](../drn-testing/SKILL.md) - Testing with contexts

---
