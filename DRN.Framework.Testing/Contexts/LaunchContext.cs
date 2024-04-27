using DotNet.Testcontainers.Containers;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;

namespace DRN.Framework.Testing.Contexts;

public static class LaunchContext
{
    /// <summary>
    /// Launches external dependencies such as postgresql and wires settings such as connection strings for dev environment if LaunchExternalDependencies feature is enabled
    /// </summary>
    public static async Task<TestcontainersHealthStatus> LaunchExternalDependenciesAsync(WebApplicationBuilder appBuilder, IAppSettings appSettings)
    {
        var features = appSettings.Features;
        var dont = TestContext.IsRunning
                   || appSettings.Environment != AppEnvironment.Development
                   || features.TemporaryApplication
                   || !features.LaunchExternalDependencies;

        if (dont) return TestcontainersHealthStatus.None;

        var postgresCollection = await PostgresContext.LaunchPostgresAsync(appBuilder);
        return postgresCollection.PostgresContainer.Health;
    }
}