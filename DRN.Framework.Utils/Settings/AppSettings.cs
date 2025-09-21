using System.Text.Json.Serialization;
using Blake3;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Ids;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Utils.Settings;

public interface IAppSettings
{
    DrnAppFeatures Features { get; }
    DrnDevelopmentSettings DevelopmentSettings { get; }
    NexusAppSettings NexusAppSettings { get; }
    AppEnvironment Environment { get; }
    bool IsDevEnvironment { get; }
    string ApplicationName { get; }
    string AppKey { get; }
    string AppHashKey { get; }
    long AppSeedLong { get; }
    int AppSeedInt { get; }

    [JsonIgnore]
    IConfiguration Configuration { get; }

    bool TryGetConnectionString(string name, out string connectionString);
    string GetRequiredConnectionString(string name);
    bool TryGetSection(string key, out IConfigurationSection section);
    IConfigurationSection GetRequiredSection(string key);
    T? GetValue<T>(string key);
    T? GetValue<T>(string key, T defaultValue);
    T? Get<T>(string key, bool errorOnUnknownConfiguration = false, bool bindNonPublicProperties = true);
    ConfigurationDebugView GetDebugView();
}

[Singleton<IAppSettings>]
public class AppSettings : IAppSettings
{
    public static IAppSettings Development(params object[] settings)
    {
        var configurationBuilder = new ConfigurationManager()
            .AddObjectToJsonConfiguration(new { Environment = "Development" });

        foreach (var setting in settings)
            configurationBuilder.AddObjectToJsonConfiguration(setting);

        return new AppSettings(configurationBuilder.Build());
    }


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
        DevelopmentSettings = Get<DrnDevelopmentSettings>(nameof(DrnDevelopmentSettings)) ?? new DrnDevelopmentSettings();
        NexusAppSettings = Get<NexusAppSettings>(nameof(Settings.NexusAppSettings)) ?? new NexusAppSettings();

        if (NexusAppSettings.AppId > SourceKnownIdUtils.MaxAppId)
            throw new ConfigurationException($"Nexus AppId must be less than 64: NexusAppId: {NexusAppSettings.AppId}");
        if (NexusAppSettings.AppInstanceId > SourceKnownIdUtils.MaxAppInstanceId)
            throw new ConfigurationException($"Nexus App Instance Id must be less than 32: NexusAppId: {NexusAppSettings.AppInstanceId}");

        AppKey = ApplicationName.ToPascalCase();
        AppHashKeyLong = ("MKA " + ApplicationName + " " + Features.SeedKey + " DRN")
            .GetHash(HashAlgorithm.Sha512, ByteEncoding.Hex)
            .Substring(18, 81)
            .GetHash(HashAlgorithm.Sha512, ByteEncoding.Hex);

        AppHashKey = AppHashKeyLong.Substring(100, 8);
        AppKey = $"{AppKey}.{AppHashKey}";
        AppSeedLong = Features.SeedKey.GenerateLongSeedFromHash();
        AppSeedInt = Features.SeedKey.GenerateIntSeedFromHash();

        var hasDefaultMacKey = NexusAppSettings.MacKeys.Any(k => k.Default);
        if (hasDefaultMacKey) return;
        if (Environment != AppEnvironment.Development)
            throw new ConfigurationException($"Default Mac Key not found for the environment: {Environment.ToString()}");

        //Even if the application is not connected to nexus, we still need to add a default Mac key to make development easier.
        var key = Hasher.Hash(AppKey.ToByteArray()).AsSpan().ToArray();
        NexusAppSettings.AddNexusMacKey(new NexusMacKey(key) { Default = true });
    }

    public DrnDevelopmentSettings DevelopmentSettings { get; }
    public DrnAppFeatures Features { get; }
    public NexusAppSettings NexusAppSettings { get; }
    public AppEnvironment Environment { get; }

    public bool IsDevEnvironment => Environment == AppEnvironment.Development;
    public string ApplicationName { get; }
    public string AppKey { get; }
    public string AppHashKey { get; }
    public string AppHashKeyLong { get; }
    public long AppSeedLong { get; }
    public int AppSeedInt { get; }

    [JsonIgnore]
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

    public T? Get<T>(string key, bool errorOnUnknownConfiguration = false, bool bindNonPublicProperties = true)
        => GetSectionOrRoot(key).Get<T>(c =>
        {
            c.BindNonPublicProperties = bindNonPublicProperties;
            c.ErrorOnUnknownConfiguration = errorOnUnknownConfiguration;
        });

    public ConfigurationDebugView GetDebugView() => new(this);

    private IConfiguration GetSectionOrRoot(string key) => string.IsNullOrEmpty(key)
        ? Configuration
        : Configuration.GetSection(key);
}