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

DRN.Framework.Utils package contains common codes for other DRN.Framework packages and projects developed with DRN.Framework.

## Module

DRN.Utils can be added with following module

```csharp
namespace DRN.Framework.Utils;

public static class UtilsModule
{
    public static IServiceCollection AddDrnUtils(this IServiceCollection collection)
    {
        collection.AddServicesWithAttributes();
        collection.AddHybridCache();
        collection.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);

        return collection;
    }
}
```

## Dependency Injection with Attributes

Each module should be created in the assembly that will be scanned.

```csharp
public static class InfraModule
{
    public static IServiceCollection AddSampleInfraServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        return sc;
    }
}
```

Services resolution for attribute based services can be validated with a single line.

```csharp
serviceProvider.ValidateServicesAddedByAttributes();
```

Attribute based dependency injection reduces wiring efforts and helps developer to focus on developing. This approach also improves service resolution
validation during startup and integration testing.

```csharp
[Theory]
[DataInlineContext]
public void Validate_Sample_Dependencies(DrnTestContext context)
{
    context.ServiceCollection.AddSampleApplicationServices();
    context.ServiceCollection.AddSampleInfraServices();
    context.ValidateServices();
}
```

### Lifetime Attributes

Example attribute usage:

```csharp
[Transient<IIndependent>]
public class Independent : IIndependent
{
}
```

Following attributes marks services with a lifetime and when service collection called with **AddServicesWithAttributes** method in the assembly marked belong
they are automatically added.

```csharp
namespace DRN.Framework.Utils.DependencyInjection.Attributes;

public class LifetimeAttribute<TService>(ServiceLifetime serviceLifetime, bool tryAdd = true, object? key = null)
    : LifetimeAttribute(serviceLifetime, typeof(TService), tryAdd, key);

public class LifetimeWithKeyAttribute<TService>(ServiceLifetime serviceLifetime, object key, bool tryAdd = true)
    : LifetimeAttribute(serviceLifetime, typeof(TService), tryAdd, key);

public class ScopedAttribute<TService>(bool tryAdd = true) : LifetimeAttribute<TService>(ServiceLifetime.Scoped, tryAdd);

public class ScopedWithKeyAttribute<TService>(object key, bool tryAdd = true) : LifetimeWithKeyAttribute<TService>(ServiceLifetime.Scoped, key, tryAdd);

public class TransientAttribute<TService>(bool tryAdd = true) : LifetimeAttribute<TService>(ServiceLifetime.Transient, tryAdd);

public class TransientWithKeyAttribute<TService>(object key, bool tryAdd = true) : LifetimeWithKeyAttribute<TService>(ServiceLifetime.Transient, key, tryAdd);

public class SingletonAttribute<TService>(bool tryAdd = true) : LifetimeAttribute<TService>(ServiceLifetime.Singleton, tryAdd);

public class SingletonWithKeyAttribute<TService>(object key, bool tryAdd = true) : LifetimeWithKeyAttribute<TService>(ServiceLifetime.Singleton, key, tryAdd);
```

### HasServiceCollectionModuleAttribute

Attributes derived from `HasServiceCollectionModuleAttribute` can be used to mark custom service collection modules.
In the following example `HasDrnContextServiceCollectionModuleAttribute` marks `DrnContext<TContext>` and its service collection module.
This way dbContexts inherited from `DrnContext` doesn't need a lifetime attribute and they can be registered by `AddServicesWithAttributes` with their custom
factory.

This offers following flexibility:

1. ServiceCollectionModule of DrnContexts are defined in `DRN.Framework.EntityFramework`
2. Inherited dbContexts are defined in user defined projects and points a module in another project.
3. `AddServicesWithAttributes` extension method defined in `DRN.Framework.Utils` registers them without depending `DRN.Framework.EntityFramework` project

```csharp
public class HasDrnContextServiceCollectionModuleAttribute : HasServiceCollectionModuleAttribute
{
    static HasDrnContextServiceCollectionModuleAttribute() =>
        ModuleMethodInfo = typeof(ServiceCollectionExtensions)
            .GetMethod(nameof(ServiceCollectionExtensions.AddDbContextsWithConventions))!;

    public override async Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider)
    {
        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        var migrate = appSettings.Configuration.GetValue(DbContextConventions.AutoMigrateDevEnvironmentKey, false);
        if (appSettings.Environment == AppEnvironment.Development && migrate && service is DbContext context)
            await context.Database.MigrateAsync();
    }
}

[HasDrnContextServiceCollectionModule]
public abstract class DrnContext<TContext> : DbContext, IDesignTimeDbContextFactory<TContext>, IDesignTimeServices where TContext : DbContext, new()
{
...
```

HasServiceCollectionModuleAttribute's PostStartupValidationAsync will be called when,
* ValidateServicesAddedByAttributes extension method called from service provider if all services resolved successfully.
* For instance, DrnContext can apply EF migrations after service provider services resolved successfully.

## Configurations

Following configuration sources can be used to add configurations from different sources

* JsonSerializerConfigurationSource converts poco objects to configuration
* RemoteJsonConfigurationSource fetches remote configuration (experimental and incomplete)

Following MountedSettingsConventions will be added to configuration.
* /appconfig/json-settings json files will be added to configuration if any exist
* /appconfig/key-per-file-settings files will be added to configuration if any exist
* IMountedSettingsConventionsOverride overrides default /appconfig location if added to service collection before host built

```csharp
namespace DRN.Framework.Utils.Settings.Conventions;

public static class MountedSettingsConventions
{
    public const string DefaultMountDirectory = "/appconfig";
    
    public static string JsonSettingsMountDirectory(string? mountDirectory = null)
        => Path.Combine(mountDirectory ?? DefaultMountDirectory, "json-settings");
    public static string KeyPerFileSettingsMountDirectory(string? mountDirectory = null)
        => Path.Combine(mountDirectory ?? DefaultMountDirectory, "key-per-file-settings");

    public static DirectoryInfo JsonSettingDirectoryInfo(string? mountDirectory = null)
        => new(JsonSettingsMountDirectory(mountDirectory));
}

public interface IMountedSettingsConventionsOverride
{
    string? MountedSettingsDirectory { get; }
}

public class MountedSettingsOverride : IMountedSettingsConventionsOverride
{
    public string? MountedSettingsDirectory { get; init; }
}
```

## AppSettings

Following IAppSettings interface is defined and can be used to obtain appsettings. It has utility methods that allow fail fast.

```csharp
namespace DRN.Framework.Utils.Settings;

public interface IAppSettings
{
    AppEnvironment Environment { get; }
    IConfiguration Configuration { get; }

    DrnAppFeatures Features { get; }
    DrnLocalizationSettings Localization { get; }
    DrnDevelopmentSettings DevelopmentSettings { get; }
    NexusAppSettings NexusAppSettings { get; }
    bool IsDevEnvironment { get; }

    /// <summary>
    ///  Default app key, can be used publicly. For example, to separate development and production data.
    /// </summary>
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
}
```

## Extension Methods

* ServiceCollectionExtensions
    * ReplaceInstance
    * ReplaceTransient
    * ReplaceScoped
    * ReplaceSingleton
    * GetAllAssignableTo<TService>
* StringExtensions
    * Parse
    * TryParse
    * ToStream
    * ToByteArray
    * ToSnakeCase
    * ToCamelCase
    * ToPascalCase
* TypeExtensions
    * GetSubTypes
    * CreateSubTypes
    * CreateSubType
    * GetTypesAssignableTo
    * GetAssemblyName
* AssemblyExtensions is merged into TypeExtensions or otherUtils
* MethodUtils (Reflection Helper)
    * InvokeMethod
    * InvokeStaticMethod
    * InvokeGenericMethod
    * InvokeStaticGenericMethod
    * FindGenericMethod
    * FindNonGenericMethod

---
**Semper Progressivus: Always Progressive**