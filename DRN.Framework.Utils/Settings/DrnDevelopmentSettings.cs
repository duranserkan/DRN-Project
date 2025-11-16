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
    /// Turns on fast prototyping for database development when <see cref="AutoMigrate"/> is on.
    /// <para>
    /// When true, any DbContext that has <c>NpgsqlDbContextOptionsAttribute.UsePrototypeMode = true</c> will be recreated if there is pending model changes.
    /// </para>
    /// <para>
    /// ⚠️ Only one DbContext should use prototyping at a time. <br/>
    /// ⚠️ Use prototyping when <see cref="LaunchExternalDependencies"/> is on.
    /// </para>
    /// </summary>
    public bool Prototype { get; init; }

    public bool BreakForUserUnhandledException { get; init; }
}