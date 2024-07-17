using Npgsql;
using Testcontainers.PostgreSql;

namespace DRN.Framework.Testing.Contexts.Postgres;

public static class PostgreSqlContainerExtensions
{
    public static string GetConnectionStringWithParameters(this PostgreSqlContainer container,
        NpgsqlConnectionStringParameters? parameters = null)
    {
        var csBuilder = GetConnectionStringBuilder(container, parameters);

        return csBuilder.ConnectionString;
    }

    public static string GetConnectionStringWithParameters(this PostgreSqlContainer container, string applicationName,
        NpgsqlConnectionStringParameters? parameters = null)
    {
        var csBuilder = GetConnectionStringBuilder(container, parameters);
        csBuilder.ApplicationName = applicationName;

        return csBuilder.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder GetConnectionStringBuilder(PostgreSqlContainer container, NpgsqlConnectionStringParameters? parameters)
    {
        var cs = container.GetConnectionString();
        var csBuilder = new NpgsqlConnectionStringBuilder(cs);
        parameters ??= PostgresContext.NpgsqlConnectionStringParameters;

        csBuilder.Multiplexing = parameters.Multiplexing;
        csBuilder.MaxAutoPrepare = parameters.MaxAutoPrepare;
        csBuilder.MaxPoolSize = parameters.MaxPoolSize;

        return csBuilder;
    }
}