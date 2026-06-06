using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Settings.Conventions;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Utils.Configurations;

public class ConfigurationDebugView
{
    public ConfigurationDebugView(IAppSettings appSettings) : this(appSettings, false)
    {
    }

    public ConfigurationDebugView(IAppSettings appSettings, bool includeRawValues = false)
    {
        Environment = appSettings.Environment;
        ApplicationName = appSettings.ApplicationName;
        ConfigMountedDirectory = appSettings.GetValue<string>("MountedSettingsDirectory")
                                 ?? MountedSettingsConventions.DefaultMountDirectory;
        RawValuesIncluded = includeRawValues && appSettings.IsDevelopmentEnvironment;

        var root = appSettings.Configuration as IConfigurationRoot;
        var collectionByProvider = new Dictionary<IConfigurationProvider, DebugViewEntry[]>(10);
        DebugViewCollectionByProvider = collectionByProvider;
        var entries = new List<DebugViewEntry>(1000);
        Entries = entries;

        if (root != null)
            RecurseChildren(root, entries, root.GetChildren(), RawValuesIncluded);
        if (entries.Count == 0) return;

        foreach (var grouping in Entries.GroupBy(e => e.Provider!))
            collectionByProvider.Add(grouping.Key, grouping.OrderBy(e => e.Path).ToArray());
    }

    public AppEnvironment Environment { get; }
    public string ApplicationName { get; }
    public string ConfigMountedDirectory { get; }
    public bool RawValuesIncluded { get; }
    public IReadOnlyList<DebugViewEntry> Entries { get; }
    public IReadOnlyDictionary<IConfigurationProvider, DebugViewEntry[]> DebugViewCollectionByProvider { get; }

    public ConfigurationDebugViewSummary ToSummary() => new(this);

    private static void RecurseChildren(IConfigurationRoot root, IList<DebugViewEntry> entries, IEnumerable<IConfigurationSection> children,
        bool includeRawValues, DebugViewEntry? parentEntry = null)
    {
        foreach (var child in children)
        {
            var valueAndProvider = GetValueAndProvider(root, child.Path);
            var value = ConfigurationDebugValueRedactor.Redact(child.Path, child.Key, valueAndProvider.Value, includeRawValues);
            var entry = new DebugViewEntry(child.Path, child.Key, value, valueAndProvider.Provider, parentEntry);

            if (entry.Provider != null)
                entries.Add(entry);

            RecurseChildren(root, entries, child.GetChildren(), includeRawValues, entry);
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

internal static class ConfigurationDebugValueRedactor
{
    private const string RedactedValue = "[redacted]";

    private static readonly string[] SensitiveSections =
    [
        "apikeys",
        "connectionstrings",
        "certificate",
        "certificates",
        "credential",
        "credentials",
        "keys",
        "mackeys",
        "privatekeys",
        "secret",
        "secretkeys",
        "secrets",
        "signingkeys"
    ];

    private static readonly string[] SensitiveContains =
    [
        "password",
        "passwd",
        "secret",
        "credential",
        "connectionstring",
        "authorization"
    ];

    private static readonly string[] SensitiveSuffixes =
    [
        "pwd",
        "token",
        "apikey",
        "key",
        "cookie",
        "keys"
    ];

    public static string? Redact(string path, string key, string? value, bool includeRawValue)
    {
        if (value == null || includeRawValue)
            return value;

        return IsSensitive(path, key) ? RedactedValue : value;
    }

    private static bool IsSensitive(string path, string key)
    {
        var segments = path.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Select(Normalize).Any(segment => SensitiveSections.Contains(segment)))
            return true;

        var normalizedKey = Normalize(key);
        return SensitiveContains.Any(marker => normalizedKey.Contains(marker))
               || SensitiveSuffixes.Any(suffix => normalizedKey.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static string Normalize(string value)
        => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
