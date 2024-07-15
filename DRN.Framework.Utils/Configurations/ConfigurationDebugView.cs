using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Settings.Conventions;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Utils.Configurations;

public class ConfigurationDebugView
{
    public ConfigurationDebugView(IAppSettings appSettings)
    {
        Environment = appSettings.Environment;
        ApplicationName = appSettings.ApplicationName;
        ConfigMountedDirectory = appSettings.GetValue<string>("MountedSettingsDirectory")
                                 ?? MountedSettingsConventions.DefaultMountDirectory;

        var root = appSettings.Configuration as IConfigurationRoot;
        var collectionByProvider = new Dictionary<IConfigurationProvider, DebugViewEntry[]>(10);
        DebugViewCollectionByProvider = collectionByProvider;
        var entries = new List<DebugViewEntry>(1000);
        Entries = new List<DebugViewEntry>(1000);

        if (root != null)
            RecurseChildren(root, entries, root.GetChildren());
        if (entries.Count == 0) return;

        foreach (var grouping in Entries.GroupBy(e => e.Provider!))
            collectionByProvider.Add(grouping.Key, grouping.OrderBy(e => e.Path).ToArray());
    }

    public AppEnvironment Environment { get; }
    public string ApplicationName { get; }
    public string ConfigMountedDirectory { get; }
    public IReadOnlyList<DebugViewEntry> Entries { get; }
    public IReadOnlyDictionary<IConfigurationProvider, DebugViewEntry[]> DebugViewCollectionByProvider { get; }

    public ConfigurationDebugViewSummary ToSummary() => new(this);

    private static void RecurseChildren(IConfigurationRoot root, IList<DebugViewEntry> entries, IEnumerable<IConfigurationSection> children,
        DebugViewEntry? parentEntry = null)
    {
        foreach (var child in children)
        {
            var valueAndProvider = GetValueAndProvider(root, child.Path);
            var entry = new DebugViewEntry(child.Path, child.Key, valueAndProvider.Value, valueAndProvider.Provider, parentEntry);

            if (entry.Provider != null)
                entries.Add(entry);
            else
                RecurseChildren(root, entries, child.GetChildren(), entry);
        }
    }

    private static (string? Value, IConfigurationProvider? Provider) GetValueAndProvider(IConfigurationRoot root, string key)
    {
        foreach (var provider in root.Providers.Reverse())
            if (provider.TryGet(key, out string? value))
                return (value, provider);

        return (null, null);
    }
}

public class DebugViewEntry(string path, string key, string? value, IConfigurationProvider? provider, DebugViewEntry? parent = null)
{
    private DebugViewEntry? Parent { get; } = parent;
    public string Path { get; } = path;
    public string Key { get; } = key;
    public string? Value { get; } = value;
    public IConfigurationProvider? Provider { get; } = provider;
}