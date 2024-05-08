namespace DRN.Framework.Utils.Settings;

/// <summary>
/// Values from DrnAppFeatures section
/// </summary>
public class DrnAppFeatures
{
    public static string GetKey(string shortKey) => $"{nameof(DrnAppFeatures)}:{shortKey}";

    /// <summary>
    /// When true application dependencies are not validated
    /// </summary>
    public bool SkipValidation { get; init; }

    /// <summary>
    /// When application is created to obtain configuration and registered services, it should mark as temporary.
    /// </summary>
    public bool TemporaryApplication { get; init; }

    /// <summary>
    /// In dev environment, Launch context launches external dependencies with required configuration for rapid application development
    /// </summary>
    public bool LaunchExternalDependencies { get; init; }

    /// <summary>
    /// When true in dev environment, after registered services validated, database migrations will be applied automatically
    /// </summary>
    public bool AutoMigrateDevEnvironment { get; init; }
}