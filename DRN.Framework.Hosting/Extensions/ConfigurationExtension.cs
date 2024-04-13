using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Settings.Conventions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Extensions;

public static class ConfigurationExtension
{
    public static IConfigurationBuilder AddMountDirectorySettings(this IConfigurationBuilder builder, IServiceCollection? sc = null)
    {
        var overrideService = sc?.BuildServiceProvider().GetService<IMountedSettingsConventionsOverride>();
        var mountOverride = overrideService?.MountedSettingsDirectory;
        if (overrideService != null)
            builder.AddObjectToJsonConfiguration(overrideService);

        builder.AddKeyPerFile(MountedSettingsConventions.KeyPerFileSettingsMountDirectory(mountOverride), true);
        var jsonDirectory = MountedSettingsConventions.JsonSettingDirectoryInfo(mountOverride);
        if (!jsonDirectory.Exists) return builder;

        foreach (var files in jsonDirectory.GetFiles())
            builder.AddJsonFile(files.FullName);

        return builder;
    }

    public static IConfigurationBuilder AddDrnSettings(this IConfigurationBuilder builder, string[]? args = null, string settingJsonName = "appsettings",
        IServiceCollection? sc = null)
    {
        if (string.IsNullOrWhiteSpace(settingJsonName))
            settingJsonName = "appsettings";

        builder.AddJsonFile($"{settingJsonName}.json", true);
        builder.AddEnvironmentVariables();
        if (args != null && args.Length > 0)
            builder.AddCommandLine(args);

        var tempSettings = new AppSettings(builder.Build());

        builder.AddJsonFile($"{settingJsonName}.{tempSettings.Environment.ToString()}.json", true);
        builder.AddMountDirectorySettings(sc);

        return builder;
    }
}