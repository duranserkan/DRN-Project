using System.Linq;
using System.Reflection;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Settings.Conventions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace DRN.Framework.Hosting.Extensions;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Mounted settings like kubernetes secrets or configmaps
    /// </summary>
    public static IConfigurationBuilder AddMountDirectorySettings(this IConfigurationBuilder builder, IServiceCollection? sc = null)
    {
        using var serviceProvider = sc?.BuildServiceProvider();
        var overrideService = serviceProvider?.GetService<IMountedSettingsConventionsOverride>();
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

    public static IConfigurationBuilder AddDrnSettings(this IConfigurationBuilder builder, string applicationName, string[]? args = null,
        string settingJsonName = "appsettings",
        IServiceCollection? sc = null)
    {
        if (string.IsNullOrWhiteSpace(settingJsonName))
            settingJsonName = "appsettings";
        var fileProvider = builder.GetFileProvider() as PhysicalFileProvider;

        var environment = GetEnvironment(settingJsonName, args, sc, fileProvider?.Root);
        builder.AddJsonFile($"{settingJsonName}.json", true);
        builder.AddJsonFile($"{settingJsonName}.{environment.ToString()}.json", true);

        if (applicationName.Length > 0)
            try
            {
                var assembly = Assembly.Load(new AssemblyName(applicationName));
                builder.AddUserSecrets(assembly, true);
            }
            catch (FileNotFoundException e)
            {
                _ = e;
            }

        builder.AddSettingsOverrides(args, sc);

        return builder;
    }

    private static void AddSettingsOverrides(this IConfigurationBuilder builder, string[]? args, IServiceCollection? sc)
    {
        builder.AddEnvironmentVariables("ASPNETCORE_");
        builder.AddEnvironmentVariables("DOTNET_");
        builder.AddEnvironmentVariables();
        builder.AddMountDirectorySettings(sc);

        if (args != null && args.Length > 0)
            builder.AddCommandLine(args);
    }

    private static AppEnvironment GetEnvironment(string settingJsonName, string[]? args, IServiceCollection? sc, string? root)
    {
        var builder = new ConfigurationBuilder();
        if (!string.IsNullOrEmpty(root))
            builder.SetBasePath(root);

        builder.AddJsonFile($"{settingJsonName}.json", true);
        builder.AddSettingsOverrides(args, sc);
        var configuration = builder.Build();

        var envString = configuration[nameof(AppSettings.Environment)];
        if (string.IsNullOrWhiteSpace(envString))
        {
            throw new ConfigurationException("Environment setting is missing. Please provide a valid Environment value (e.g. Development, Staging, Production) in appsettings.json or via environment variables.");
        }

        var validNamesArray = Enum.GetNames<AppEnvironment>().Where(n => n != nameof(AppEnvironment.NotDefined)).ToArray();
        if (!validNamesArray.Contains(envString, StringComparer.OrdinalIgnoreCase))
        {
            var validNames = string.Join(", ", validNamesArray);
            throw new ConfigurationException($"Invalid Environment value: '{envString}'. Valid values are: {validNames}.");
        }

        return Enum.Parse<AppEnvironment>(envString, true);
    }
}
