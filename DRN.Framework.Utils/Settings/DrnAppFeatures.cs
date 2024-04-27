using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Utils.Settings;

public class DrnAppFeatures
{
    public static string GetKey(string shortKey) => $"{nameof(DrnAppFeatures)}:{shortKey}";

    public DrnAppFeatures(IAppSettings appSettings)
    {
        var hasFeatures = appSettings.TryGetSection(nameof(DrnAppFeatures), out var features);
        if (!hasFeatures) return;

        SkipValidation = features.GetValue(nameof(SkipValidation), false);
        TemporaryApplication = features.GetValue(nameof(TemporaryApplication), false);
        LaunchExternalDependencies = features.GetValue(nameof(LaunchExternalDependencies), false);
        AutoMigrateDevEnvironment = features.GetValue(nameof(AutoMigrateDevEnvironment), false);
    }

    public bool SkipValidation { get; }
    public bool TemporaryApplication { get; }
    public bool LaunchExternalDependencies { get; }
    public bool AutoMigrateDevEnvironment { get; }
}