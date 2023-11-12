using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Utils.Settings;

public interface IAppSettings
{
    IConfiguration Configuration { get; }
    bool TryGetConnectionString(string name, out string connectionString);
    string GetRequiredConnectionString(string name);
    bool TryGetSection(string key, out IConfigurationSection section);
    IConfigurationSection GetRequiredSection(string key);
}

[LifetimeSingleton<IAppSettings>]
public class AppSettings : IAppSettings
{
    public AppSettings(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public bool TryGetConnectionString(string name, out string connectionString)
    {
        connectionString = Configuration.GetConnectionString(name)!;
        return !string.IsNullOrWhiteSpace(connectionString);
    }

    public string GetRequiredConnectionString(string name)
    {
        var connectionString = Configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new ConfigurationException($"{name} connection string not found")
            : connectionString;
    }

    public bool TryGetSection(string key, out IConfigurationSection section)
    {
        section = Configuration.GetSection(key);
        return section.Exists();
    }


    public IConfigurationSection GetRequiredSection(string key)
    {
        var section = Configuration.GetSection(key);
        return section.Exists()
            ? section
            : throw new ConfigurationException($"{key} configuration section not found");
    }
}