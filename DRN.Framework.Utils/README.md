# DRN.Framework.Utils
DRN.Framework.Utils package contains common codes for other DRN.Framework packages, projects developed with DRN.Framework.

## Module
DRN.Utils can be added with following module

```csharp
namespace DRN.Framework.Utils;

public static class UtilsModule
{
    public static IServiceCollection AddDrnUtils(this IServiceCollection collection)
    {
        collection.TryAddSingleton<IAppSettings, AppSettings>();

        return collection;
    }
}
```

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