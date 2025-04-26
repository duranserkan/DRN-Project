namespace DRN.Framework.Utils.Settings;

/// <summary>
/// Values from DrnAppFeatures section
/// </summary>
public class DrnAppFeatures
{
    public static string GetKey(string shortKey) => $"{nameof(DrnAppFeatures)}:{shortKey}";

    /// <summary>
    /// When true application dependencies are not validated. Mostly, used by Test Contexts.
    /// </summary>
    public bool SkipValidation { get; init; }

    /// <summary>
    /// When application is created to obtain configuration and registered services, it should mark as temporary. Mostly, used by Test Contexts.
    /// </summary>
    public bool TemporaryApplication { get; init; }

    /// <summary>
    /// Shows which test started the application when application is created with TestContext for integration tests.
    /// </summary>
    public string? ApplicationStartedBy { get; init; }

    /// <summary>
    /// In dev environment, Launch context launches external dependencies with required configuration for rapid application development
    /// </summary>
    public bool LaunchExternalDependencies { get; init; }

    /// <summary>
    /// When true in dev environment, after registered services validated, database migrations will be applied automatically for rapid application development
    /// </summary>
    public bool AutoMigrateDevEnvironment { get; init; }

    /// <summary>
    /// When true in dev environment and AutoMigrateDevEnvironment is enabled, migrations are not used for database creation in order to increase prototyping speed.
    /// For each DbContext, a new database will be created except when database exists and there are no pending changes.
    /// If multiple DbContexts needs to share same database do not use prototyping mode.
    /// </summary>
    public bool PrototypingMode { get; init; }

    public bool SeedData { get; init; }
    public string SeedKey { get; init; } = "Peace at home, peace in the world — Mustafa Kemal Atatürk";
    public string InternalRequestHttpVersion { get; init; } = "1.1";
    public string InternalRequestProtocol { get; init; } = "http";
    public bool UseHttpRequestLogger { get; init; } = false;
}