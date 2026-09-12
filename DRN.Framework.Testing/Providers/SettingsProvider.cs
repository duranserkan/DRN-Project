using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Testing.Providers;

public static class SettingsProvider
{
    public const string ConventionSettingsName = "settings";
    public const string ConventionDirectory = "Settings";
    public static readonly string GlobalConventionLocation = Path.Combine(Directory.GetCurrentDirectory(), ConventionDirectory);

    /// <summary>Creates standalone development settings for the default partition (AppId 0), rejecting conflicting AppId overrides.</summary>
    public static AppSettings Development(params object[] settings) => Development<DefaultApp>(settings);

    /// <summary>Creates standalone development settings for the declared application partition, rejecting conflicting AppId overrides.</summary>
    public static AppSettings Development<TApp>(params object[] settings) where TApp : IAppId
    {
        var appId = TApp.AppId;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appId, IAppId.MaxAppId);
        var builder = new ConfigurationManager().AddObjectToJsonConfiguration(new
        {
            Environment = "Development",
            NexusAppSettings = new { AppId = appId, AppInstanceId = 0 }
        });
        foreach (var setting in settings)
            builder.AddObjectToJsonConfiguration(setting);

        var configuration = builder.Build();
        if (!byte.TryParse(configuration["NexusAppSettings:AppId"], out var configuredAppId) || configuredAppId != appId)
            throw ExceptionFor.Configuration($"NexusAppSettings:AppId must match the declared AppId {appId} for {typeof(TApp).Name}.");

        return new AppSettings(configuration);
    }

    /// <summary>
    /// Creates <see cref="IAppSettings"/> from settings JSON file found in provided location.
    /// Alternate locations are the Settings subfolder of the test project or provided location by convention
    /// Make sure file is copied to output directory, extension is JSON and settings name referring to it should not end with .json
    /// </summary>
    public static IAppSettings GetAppSettings(string settingsName = ConventionSettingsName, string? settingsDirectoryPath = null,
        List<IConfigurationSource>? configurationSources = null) =>
        new AppSettings(GetConfiguration(settingsName, settingsDirectoryPath, configurationSources));

    /// <summary>
    /// Creates <see cref="IConfiguration"/> from settings JSON file found in provided location.
    /// By convention, alternate locations are the Settings subdirectory of the test project or the provided location
    /// Make sure file is copied to output directory, extension is JSON and settings name referring to it should not end with .json
    /// </summary>
    public static IConfiguration GetConfiguration(string settingsJsonName = ConventionSettingsName, string? settingsDirectoryPath = null,
        List<IConfigurationSource>? configurationSources = null, IServiceCollection? serviceCollection = null)
    {
        var settingsPath = GetSettingsPath(settingsJsonName, settingsDirectoryPath);
        var configurationBuilder = new ConfigurationBuilder().SetBasePath(settingsPath.SelectedDirectory)
            .AddDrnSettings(AppConstants.EntryAssemblyName, settingJsonName: settingsJsonName, sc: serviceCollection);

        foreach (var source in configurationSources ?? [])
            configurationBuilder.Add(source);

        return configurationBuilder.Build();
    }

    public static DataProviderResultDataPath GetSettingsPath(string settingsName = ConventionSettingsName, string? settingsDirectoryPath = null)
    {
        var settingsRelativePath = Path.HasExtension(settingsName) ? settingsName : $"{settingsName}.json";
        var settingsPath = DataProvider.GetDataPath(settingsRelativePath, settingsDirectoryPath, ConventionDirectory);

        return settingsPath;
    }

    /// <summary>
    /// Gets the content of specified data file in the Settings directory.
    /// Settings directory must be created in the root of the test Project or provided location.
    /// </summary>
    /// <param name="settingsName">
    /// Path is relative Settings directory. If no extension is provided default .json extension will be used.
    /// Make sure the settings file is copied to output directory.
    /// </param>
    /// <param name="settingsDirectoryPath">If not provided global convention location will be applied</param>
    public static DataProviderResult GetSettingsData(string settingsName = ConventionSettingsName, string? settingsDirectoryPath = null)
    {
        var settingsRelativePath = Path.HasExtension(settingsName) ? settingsName : $"{settingsName}.json";
        var settingsData = DataProvider.Get(settingsRelativePath, settingsDirectoryPath, ConventionDirectory);

        return settingsData;
    }
}
