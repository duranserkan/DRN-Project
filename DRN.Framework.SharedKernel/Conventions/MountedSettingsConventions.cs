namespace DRN.Framework.SharedKernel.Conventions;

public static class MountedSettingsConventions
{
    public const string DefaultMountDirectory = "/appconfig";

    public static string JsonSettingsMountDirectory(string? mountDirectory = null)
        => Path.Combine(mountDirectory ?? DefaultMountDirectory, "json-settings");

    public static string KeyPerFileSettingsMountDirectory(string? mountDirectory = null)
        => Path.Combine(mountDirectory ?? DefaultMountDirectory, "key-per-file-settings");

    public static DirectoryInfo JsonSettingDirectoryInfo(string? mountDirectory = null)
        => new(JsonSettingsMountDirectory(mountDirectory));
}

public interface IMountedSettingsConventionsOverride
{
    string? MountDirectory { get; }
}

public class MountedSettingsOverride : IMountedSettingsConventionsOverride
{
    public string? MountDirectory { get; set; }
}