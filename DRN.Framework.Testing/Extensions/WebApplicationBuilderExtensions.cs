using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;

namespace DRN.Framework.Testing.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Launches external dependencies in a container such as postgresql and wires settings such as connection strings for dev environment if LaunchExternalDependencies feature is enabled
    /// </summary>
    public static async Task<ExternalDependencyLaunchResult> LaunchExternalDependenciesAsync(
        this WebApplicationBuilder builder, IScopedLog? scopedLog, IAppSettings appSettings,
        ExternalDependencyLaunchOptions? options = null)
    {
        scopedLog?.AddToActions("Launching External dependencies...");

        options ??= new ExternalDependencyLaunchOptions();
        var result = new ExternalDependencyLaunchResult(appSettings);
        if (!result.Launched) return result;

        var postgresCollection = await PostgresContext.LaunchPostgresAsync(builder, options);
        result.PostgresCollection = postgresCollection;

        scopedLog?.AddToActions("External dependencies launched");
        scopedLog?.Add(nameof(result.PostgresConnection), result.PostgresConnection);

        return result;
    }
}

public class ExternalDependencyLaunchOptions
{
    public PostgresContainerSettings PostgresContainerSettings { get; init; } = new()
    {
        Reuse = true,
        HostPort = 5432
    };
}

public class ExternalDependencyLaunchResult(IAppSettings appSettings)
{
    public PostgresCollection? PostgresCollection { get; internal set; }
    public string PostgresConnection => PostgresCollection?.PostgresContainer.GetConnectionString() ?? "";

    /// <summary>
    /// External dependencies will be launched as test containers for development purposes when following conditions satisfied:
    /// <list type="bullet|number|table">
    /// <item>
    /// <term>Environment</term>
    /// <description>is Development</description>
    /// </item>
    /// <item>
    /// <term>LaunchFlag</term>
    /// <description>is true(Check AppSettings.Features.LaunchExternalDependencies).</description>
    /// </item>
    /// <item>
    /// <term>Application</term>
    /// <description>is not created in a test(TestContext.IsRunning should be false)</description>
    /// </item>
    /// <item>
    /// <term>Application</term>
    /// <description>is not temporary(Check AppSettings.Features.TemporaryApplication)</description>
    /// </item>
    /// </list>
    /// </summary>
    public bool Launched { get; } = !TestEnvironment.TestContextEnabled
                                    && appSettings.IsDevEnvironment
                                    && appSettings.Features.LaunchExternalDependencies
                                    && !appSettings.Features.TemporaryApplication;
}