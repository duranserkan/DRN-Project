using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Testing.Providers;

public static class SettingsProvider
{
    public static readonly string ConventionDirectory = "Settings";
    public static readonly string GlobalConventionLocation = Path.Combine(Directory.GetCurrentDirectory(), ConventionDirectory);

    /// <summary>
    /// Creates <see cref="IAppSettings"/> from settings json file found in provided location.
    /// Alternate locations are the Settings subfolder of the test project or provided location by convention
    /// Make sure file is copied to output directory, extension is json and settings name referring to it should not end with .json
    /// </summary>
    public static IAppSettings GetAppSettings(string settingsName = "settings", string? location = null,
        List<IConfigurationSource>? configurationSources = null) =>
        new AppSettings(GetConfiguration(settingsName, location, configurationSources));

    /// <summary>
    /// Creates <see cref="IConfiguration"/> from settings json file found in provided location.
    /// Alternate locations are the Settings subfolder of the test project or provided location by convention
    /// Make sure file is copied to output directory, extension is json and settings name referring to it should not end with .json
    /// </summary>
    public static IConfiguration GetConfiguration(string settingsName = "settings", string? location = null,
        List<IConfigurationSource>? configurationSources = null)
    {
        var settingsJson = $"{settingsName}.json";
        var locationFound = CheckLocation(location, settingsJson);
        if (!locationFound && !string.IsNullOrWhiteSpace(location))
        {
            //alternate location by convention
            location = Path.Combine(location, ConventionDirectory);
            locationFound = CheckLocation(location, settingsJson);
        }

        var selectedLocation = locationFound ? location! : GlobalConventionLocation;

        var configurationBuilder = new ConfigurationBuilder().SetBasePath(selectedLocation).AddJsonFile(settingsJson);

        if (configurationSources != null)
            foreach (var source in configurationSources)
                configurationBuilder.Add(source);


        return configurationBuilder.Build();
    }

    private static bool CheckLocation(string? location, string settingsJson)
        => !string.IsNullOrWhiteSpace(location) && File.Exists(Path.Combine(location, settingsJson));
}