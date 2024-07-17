using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Testing.Contexts.Startup;

namespace DRN.Test;

public class TestStartupJob : ITestStartupJob
{
    public void Run(StartupContext context)
    {
        NpgsqlConnectionStringParameters.DefaultMaxPoolSize = 10;
        PostgresContext.NpgsqlConnectionStringParameters = new();

        PostgresContainerSettings.DefaultPassword = "DrnStartUp";
        PostgresContext.PostgresContainerSettings = new();

        var dataResult = context.GetData("StartUpData.txt");
        dataResult.Data.Should().Be("Peace at Home, Peace in the World");
    }
}