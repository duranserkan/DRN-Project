using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Configurations;
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
    T? GetValue<T>(string key);
    T? GetValue<T>(string key, T defaultValue);
    ConfigurationDebugView GetDebugView();
}

[Singleton<IAppSettings>]
public class AppSettings : IAppSettings
{
    public AppSettings(IConfiguration configuration)
    {
        Configuration = configuration;
        Environment = TryGetSection(nameof(Environment), out _)
            ? configuration.GetValue<AppEnvironment>(nameof(Environment))
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

    public T? GetValue<T>(string key) => Configuration.GetValue<T>(key);
    public T? GetValue<T>(string key, T defaultValue) => Configuration.GetValue(key, defaultValue);
    public ConfigurationDebugView GetDebugView() => new(this);
}