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

        return collection;
    }
}
```
## Dependency Injection with Attributes
Example attribute usage:
```csharp
[Transient<IIndependent>]
public class Independent : IIndependent
{
}
```

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

Services resolution for attribute based services can be validated with single line
```csharp
serviceProvider.ValidateServicesAddedByAttributes();
```

Attribute based dependency injection reduces wiring efforts and helps developer to focus on developing. This approach also improves service resolution validation during startup and integration testing.
```csharp
    [Theory]
    [DataInlineContext]
    public void Validate_Sample_Dependencies(TestContext context)
    {
        context.ServiceCollection.AddSampleApplicationServices();
        context.ServiceCollection.AddSampleInfraServices();
        context.ValidateServices();
    }
```

Following attributes marks services with a lifetime and when service collection called with *AddServicesWithAttributes* method in the assembly marked belong they are automatically added.
```csharp
namespace DRN.Framework.Utils.DependencyInjection;

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

## Configurations

Following configuration sources can be used to add configurations from different sources
* JsonSerializerConfigurationSource converts poco objects to configuration
* RemoteJsonConfigurationSource fetches remote configuration (experimental and incomplete)

## AppSettings
Following IAppSettings interface is defined and can be used to obtain appsettings. It has utility methods that allow fail fast.
```csharp
namespace DRN.Framework.Utils.Settings;

public interface IAppSettings
{
    IConfiguration Configuration { get; }
    bool TryGetConnectionString(string name, out string connectionString);
    string GetRequiredConnectionString(string name);
    bool TryGetSection(string key, out IConfigurationSection section);
    IConfigurationSection GetRequiredSection(string key);
}
```

## ExtensionMethods
* ServiceCollectionExtensions
  * ReplaceInstance
  * ReplaceTransient
  * ReplaceScoped
  * ReplaceSingleton
* StringExtensions
  * ToStream