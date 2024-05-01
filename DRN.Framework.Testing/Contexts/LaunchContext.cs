using System.Diagnostics;
using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Serilog;

namespace DRN.Framework.Testing.Contexts;

public static class LaunchContext
{
    /// <summary>
    /// Launches external dependencies in a container such as postgresql and wires settings such as connection strings for dev environment if LaunchExternalDependencies feature is enabled
    /// </summary>
    public static async Task<ExternalDependencyLaunchResult> LaunchExternalDependenciesAsync(
        this WebApplicationBuilder builder,
        ExternalDependencyLaunchOptions? options = null,
        IAppSettings? appSettings = null)
    {
        options ??= new ExternalDependencyLaunchOptions();
        var result = new ExternalDependencyLaunchResult(appSettings ?? AppSettings.Instance!);
        if (!result.Launched) return result;

        var postgresCollection = await PostgresContext.LaunchPostgresAsync(builder, options);
        result.PostgresCollection = postgresCollection;

        return result;
    }
}

public class ExternalDependencyLaunchOptions
{
    public bool ContainerReuse { get; init; } = true;
    public int PostgresHostPort { get; init; } = 5432;
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
    public bool Launched { get; } = TestContext.IsRunning
                                    || appSettings.IsDevEnvironment
                                    || appSettings.Features.TemporaryApplication
                                    || !appSettings.Features.LaunchExternalDependencies;
}