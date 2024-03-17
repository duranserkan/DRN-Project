using System.Text.Json.Serialization;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Settings.Conventions;

namespace DRN.Framework.Utils.Configurations;

public class ConfigurationDebugViewSummary
{
    [JsonConstructor]
    private ConfigurationDebugViewSummary()
    {
    }

    public ConfigurationDebugViewSummary(ConfigurationDebugView configurationDebugView)
    {
        AppEnvironment = configurationDebugView.Environment;
        AppName = AppConstants.ApplicationName;
        ConfigMountedDirectory = configurationDebugView.ConfigMountedDirectory;

        var collectionByProvider = new Dictionary<string, string[]>(10);
        SettingsByProvider = collectionByProvider;

        var items = configurationDebugView.Entries.Select(entry => new DebugViewSummaryItem(entry));
        foreach (var grouping in items.GroupBy(e => e.Provider))
            collectionByProvider.Add(grouping.Key, grouping.OrderBy(item => item.Path).Select(item => item.ToString()).ToArray());

        var mountDirectory = configurationDebugView.ConfigMountedDirectory;
        ConfigMountedDirectoryJsonFiles = GetDirectoryFileNames(MountedSettingsConventions.JsonSettingsMountDirectory(mountDirectory));
        ConfigMountedDirectoryKeyPerFiles = GetDirectoryFileNames(MountedSettingsConventions.KeyPerFileSettingsMountDirectory(mountDirectory));
    }

    public string AppName { get; init; } = default!;
    public AppEnvironment AppEnvironment { get; init; }
    public string? ConfigMountedDirectory { get; init; } = default!;
    public string[] ConfigMountedDirectoryJsonFiles { get; init; } = default!;
    public string[] ConfigMountedDirectoryKeyPerFiles { get; init; } = default!;
    public IReadOnlyDictionary<string, string[]> SettingsByProvider { get; init; } = default!;

    private string[] GetDirectoryFileNames(string directory)
    {
        var directoryInfo = new DirectoryInfo(directory);
        var files = directoryInfo.Exists
            ? directoryInfo.EnumerateFiles().Select(f => f.Name).ToArray()
            : ["n/a"];

        return files;
    }
}

public class DebugViewSummaryItem(DebugViewEntry entry)
{
    public string Path { get; } = entry.Path;
    public string? Value { get; } = entry.Value;
    public string Provider { get; } = entry.Provider?.ToString() ?? "n/a";
    public override string ToString() => $"{Path}={Value}";
}