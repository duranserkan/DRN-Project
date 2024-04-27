using DRN.Framework.EntityFramework;
using Testcontainers.PostgreSql;

namespace DRN.Framework.Testing.Contexts.Postgres;

public class PostgresCollection(DbContextCollection DbContextCollection, PostgreSqlContainer PostgresContainer)
{
    public DbContextCollection DbContextCollection { get; } = DbContextCollection;
    public PostgreSqlContainer PostgresContainer { get; } = PostgresContainer;
}