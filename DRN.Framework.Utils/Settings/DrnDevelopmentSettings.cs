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
    /// It is true by default, When true in development-environment, after registered services validated, database migrations will be applied automatically for rapid application development
    /// </summary>
    public bool AutoMigrateDevelopment { get; init; } = true;
    
    /// <summary>
    /// It is false by default, When true in staging-environment, after registered services validated, database migrations will be applied automatically.
    /// </summary>
    public bool AutoMigrateStaging { get; init; } = false;

    /// <summary>
    /// Turns on development-only fast prototyping for database development when <see cref="AutoMigrateDevelopment"/> is on.
    /// <para>
    /// When true in the Development environment, any DbContext that has <c>NpgsqlDbContextOptionsAttribute.UsePrototypeMode = true</c> will be recreated if there are pending model changes.
    /// Staging auto-migration applies migrations only and never enables prototype recreation.
    /// </para>
    /// <para>
    /// ⚠️ Only one DbContext should use prototyping at a time. <br/>
    /// ⚠️ Use prototyping when <see cref="LaunchExternalDependencies"/> is on.
    /// </para>
    /// </summary>
    public bool Prototype { get; init; }

    public bool BreakForUserUnhandledException { get; init; }
}
