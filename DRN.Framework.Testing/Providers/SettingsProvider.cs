using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Testing.Providers;

public static class SettingsProvider
{
    /// <summary>
    /// Creates <see cref="IAppSettings"/> from settings json file found in the settings folder of the test project
    /// Make sure file is copied to output directory, extension is json and settings name referring to it should not end with .json
    /// </summary>
    public static IAppSettings GetAppSettings(string settingsName = "defaultAppSettings") => new AppSettings(GetConfiguration(settingsName));

    /// <summary>
    /// Creates <see cref="IConfiguration"/> from settings json file found in the settings folder of the test project
    /// Make sure file is copied to output directory, extension is json and settings name referring to it should not end with .json
    /// </summary>
    public static IConfiguration GetConfiguration(string settingsName = "defaultAppSettings") =>
        new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(Path.Combine("Settings", $"{settingsName}.json")).Build();
}