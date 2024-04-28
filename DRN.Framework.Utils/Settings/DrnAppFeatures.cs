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

    /// <summary>
    /// When true application dependencies are not validated
    /// </summary>
    public bool SkipValidation { get; }

    /// <summary>
    /// When application is created to obtain configuration and registered services, it should mark as temporary.
    /// </summary>
    public bool TemporaryApplication { get; }

    /// <summary>
    /// In dev environment, Launch context launches external dependencies with required configuration for rapid application development
    /// </summary>
    public bool LaunchExternalDependencies { get; }

    /// <summary>
    /// When true in dev environment, after registered services validated, database migrations will be applied automatically
    /// </summary>
    public bool AutoMigrateDevEnvironment { get; }
}