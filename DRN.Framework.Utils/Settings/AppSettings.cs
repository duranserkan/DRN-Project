using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Utils.Settings;

public interface IAppSettings
{
    AppEnvironment Environment { get; }
    IConfiguration Configuration { get; }
    bool TryGetConnectionString(string name, out string connectionString);
    string GetRequiredConnectionString(string name);
    bool TryGetSection(string key, out IConfigurationSection section);
    IConfigurationSection GetRequiredSection(string key);
}

[Singleton<IAppSettings>]
public class AppSettings : IAppSettings
{
    public AppSettings(IConfiguration configuration)
    {
        Configuration = configuration;
        Environment = TryGetSection("environment", out _)
            ? configuration.GetValue<AppEnvironment>("environment")
            : AppEnvironment.NotDefined;
    }

    public AppEnvironment Environment { get; }
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