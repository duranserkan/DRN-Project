using System.Text.Json.Serialization;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Utils.Settings;

public interface IAppSettings
{
    DrnAppFeatures Features { get; }
    AppEnvironment Environment { get; }
    bool IsDevEnvironment { get; }
    string ApplicationName { get; }
    string AppKey { get; }
    string AppHashKey { get; }
    long AppSeedLong { get; }
    int AppSeedInt { get; }

    [JsonIgnore] IConfiguration Configuration { get; }
    bool TryGetConnectionString(string name, out string connectionString);
    string GetRequiredConnectionString(string name);
    bool TryGetSection(string key, out IConfigurationSection section);
    IConfigurationSection GetRequiredSection(string key);
    T? GetValue<T>(string key);
    T? GetValue<T>(string key, T defaultValue);
    T? Get<T>(string key, Action<BinderOptions>? configureOptions = null);
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
        ApplicationName = TryGetSection(nameof(ApplicationName), out _)
            ? configuration.GetValue<string>(nameof(ApplicationName)) ?? AppConstants.EntryAssemblyName
            : AppConstants.EntryAssemblyName;
        Features = Get<DrnAppFeatures>(nameof(DrnAppFeatures)) ?? new DrnAppFeatures();

        AppKey = ApplicationName.ToPascalCase();
        AppHashKey = ("MKA " + ApplicationName + " " + Features.SeedKey + " DRN")
            .GetSha512Hash().Substring(18, 81)
            .GetSha512Hash().Substring(19, 23)
            .GetSha512Hash().Substring(20, 23)
            .GetSha512Hash().Substring(100, 8);
        AppKey = $"{AppKey}.{AppHashKey}";
        AppSeedLong = Features.SeedKey.GenerateLongSeedFromHash();
        AppSeedInt = Features.SeedKey.GenerateIntSeedFromHash();
    }

    public DrnAppFeatures Features { get; }
    public AppEnvironment Environment { get; }

    public bool IsDevEnvironment => Environment == AppEnvironment.Development;
    public string ApplicationName { get; }
    public string AppKey { get; }
    public string AppHashKey { get; }
    public long AppSeedLong { get; }
    public int AppSeedInt { get; }
    [JsonIgnore] public IConfiguration Configuration { get; }

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

    public T? Get<T>(string key, Action<BinderOptions>? configureOptions = null)
        => Configuration.GetSection(key).Get<T>(configureOptions);

    public ConfigurationDebugView GetDebugView() => new(this);
}