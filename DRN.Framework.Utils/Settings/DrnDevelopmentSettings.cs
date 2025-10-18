using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Settings;

[Config]
public class DrnDevelopmentSettings
{
    public static string GetKey(string shortKey) => $"{nameof(DrnDevelopmentSettings)}:{shortKey}";

    /// <summary>
    /// When true application dependencies are not validated. Mostly, used by Test Contexts.
    /// </summary>
    public bool SkipValidation { get; init; }

    /// <summary>
    /// When an application is created to get configuration and registered services, it should mark as temporary. Mostly, used by Test Contexts.
    /// </summary>
    public bool TemporaryApplication { get; init; }

    /// <summary>
    /// In dev environment, Launch context launches external dependencies with required configuration for rapid application development
    /// </summary>
    public bool LaunchExternalDependencies { get; init; }

    /// <summary>
    /// When true in dev-environment, after registered services validated, database migrations will be applied automatically for rapid application development
    /// </summary>
    public bool AutoMigrate { get; init; }

    /// <summary>
    /// When true in dev environment and AutoMigrateDevEnvironment is enabled, migrations are not used for database creation to increase prototyping speed.
    /// For each DbContext, a new database will be created except when the database exists and there are no pending changes.
    /// If multiple DbContexts need to share the same database, do not use prototyping mode.
    /// </summary>
    public bool Prototype { get; init; }
}