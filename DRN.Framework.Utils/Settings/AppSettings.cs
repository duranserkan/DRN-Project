using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Data.Hashing;
using DRN.Framework.Utils.Data.Validation;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Ids;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Utils.Settings;

public interface IAppSettings
{
    [JsonIgnore]
    IConfiguration Configuration { get; }

    DrnAppFeatures Features { get; }
    DrnLocalizationSettings Localization { get; }
    DrnDevelopmentSettings DevelopmentSettings { get; }
    NexusAppSettings NexusAppSettings { get; }
    AppEnvironment Environment { get; }
    bool IsDevelopmentEnvironment { get; }
    bool IsStagingEnvironment { get; }

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
    ConfigurationDebugView GetDebugView(bool includeRawValues);
}

[Singleton<IAppSettings>]
public class AppSettings : IAppSettings, IDisposable
{
    private const string LegacyNexusMacKeysSection = "NexusAppSettings:MacKeys";
    private const string NexusKeysSection = "NexusAppSettings:Keys";
    private const string DevelopmentNexusKeyMaterialContext =
        "DRN.Framework.Utils Development NexusKey material from 1881 to 193∞ Forever 2026-06-29 21:57:43 v1";

    public static IAppSettings Development(params object[] settings)
    {
        var configurationBuilder = new ConfigurationManager().AddObjectToJsonConfiguration(new { Environment = "Development" });

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
        Features.ValidateDataAnnotationsThrowIfInvalid();

        Localization = Get<DrnLocalizationSettings>(nameof(DrnLocalizationSettings)) ?? new DrnLocalizationSettings();
        Localization.ValidateDataAnnotationsThrowIfInvalid();

        DevelopmentSettings = Get<DrnDevelopmentSettings>(nameof(DrnDevelopmentSettings)) ?? new DrnDevelopmentSettings();
        DevelopmentSettings.ValidateDataAnnotationsThrowIfInvalid();

        NexusAppSettings = Get<NexusAppSettings>(nameof(NexusAppSettings)) ?? new NexusAppSettings();
        try
        {
            ThrowIfLegacyNexusMacKeysConfigured();
            var securitySettings = new AppSecuritySettings(Features);
            AppKey = securitySettings.AppKey;

            if (NexusAppSettings.AppId > SourceKnownIdUtils.MaxAppId)
                throw ExceptionFor.Configuration($"Nexus AppId must be less than or equal to {SourceKnownIdUtils.MaxAppId}: NexusAppId: {NexusAppSettings.AppId}");
            if (NexusAppSettings.AppInstanceId > SourceKnownIdUtils.MaxAppInstanceId)
                throw ExceptionFor.Configuration(
                    $"Nexus App Instance Id must be less than or equal to {SourceKnownIdUtils.MaxAppInstanceId}: NexusAppInstanceId: {NexusAppSettings.AppInstanceId}");

            ApplicationNameNormalized = ApplicationName.ToPascalCase();

            var hasDefaultNexusKey = NexusAppSettings.HasDefaultKey();
            if (!hasDefaultNexusKey)
            {
                if (Environment != AppEnvironment.Development)
                    throw ExceptionFor.Configuration($"Default Nexus key not found for the environment: {Environment.ToString()}");

                // Even if the application is not connected to Nexus, development still needs deterministic key material.
                var keyMaterial = DeriveDevelopmentNexusKeyMaterial(securitySettings);
                NexusAppSettings.AddNexusKey(new NexusKey(keyMaterial, ByteEncoding.Base64UrlEncoded) { Default = true });
            }

            NexusAppSettings.Validate();
        }
        catch
        {
            NexusAppSettings.Dispose();
            throw;
        }
    }

    [JsonIgnore]
    public IConfiguration Configuration { get; }

    public DrnLocalizationSettings Localization { get; }
    public DrnDevelopmentSettings DevelopmentSettings { get; }
    public DrnAppFeatures Features { get; }
    public NexusAppSettings NexusAppSettings { get; }
    public AppEnvironment Environment { get; }

    public bool IsDevelopmentEnvironment => Environment == AppEnvironment.Development;
    public bool IsStagingEnvironment => Environment == AppEnvironment.Staging;

    /// <summary>
    ///  Default app key, can be used publicly. For example, to separate development and production data.
    /// </summary>
    public string AppKey { get; }

    public string ApplicationName { get; }
    public string ApplicationNameNormalized { get; }
    public string GetAppSpecificName(string name, string prefix = "_") => $"{prefix}{ApplicationNameNormalized}.{name}.{AppKey}";

    public void Dispose() => NexusAppSettings.Dispose();

    public bool TryGetConnectionString(string name, out string connectionString)
    {
        connectionString = Configuration.GetConnectionString(name)!;
        return !string.IsNullOrWhiteSpace(connectionString);
    }

    public string GetRequiredConnectionString(string name)
    {
        var connectionString = Configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw ExceptionFor.Configuration($"{name} connection string not found")
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
            : throw ExceptionFor.Configuration($"{key} configuration section not found");
    }

    public T? GetValue<T>(string key) => Configuration.GetValue<T>(key);
    public T? GetValue<T>(string key, T defaultValue) => Configuration.GetValue(key, defaultValue);

    public T? Get<T>(string key, bool errorOnUnknownConfiguration = false, bool bindNonPublicProperties = true)
        => GetSectionOrRoot(key).Get<T>(c =>
        {
            c.BindNonPublicProperties = bindNonPublicProperties;
            c.ErrorOnUnknownConfiguration = errorOnUnknownConfiguration;
        });

    public ConfigurationDebugView GetDebugView() => GetDebugView(false);
    public ConfigurationDebugView GetDebugView(bool includeRawValues) => new(this, includeRawValues);

    private void ThrowIfLegacyNexusMacKeysConfigured()
    {
        if (!Configuration.GetSection(LegacyNexusMacKeysSection).Exists())
            return;

        throw ExceptionFor.Configuration(
            $"{LegacyNexusMacKeysSection} is no longer supported. " +
            $"Migrate {LegacyNexusMacKeysSection}[*].Key to {NexusKeysSection}[*].KeyMaterial, " +
            $"and move Format and Default to the matching {NexusKeysSection}[*].Format and {NexusKeysSection}[*].Default entries.");
    }

    private static string DeriveDevelopmentNexusKeyMaterial(AppSecuritySettings securitySettings)
    {
        var developmentKeyMaterialSeed = Encoding.UTF8.GetBytes($"{securitySettings.AppHashKey}:{securitySettings.AppEncryptionKey}:{securitySettings.AppKey}");

        try
        {
            using var keyMaterial = Blake3KeyDerivation.Derive32ByteKey(
                developmentKeyMaterialSeed,
                DevelopmentNexusKeyMaterialContext);

            return keyMaterial.Span.Encode(ByteEncoding.Base64UrlEncoded);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(developmentKeyMaterialSeed);
        }
    }

    private IConfiguration GetSectionOrRoot(string key) => string.IsNullOrEmpty(key)
        ? Configuration
        : Configuration.GetSection(key);
}
