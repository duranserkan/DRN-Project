using DRN.Framework.EntityFramework;
using Testcontainers.PostgreSql;

namespace DRN.Framework.Testing.Contexts.Postgres;

public class PostgresCollection(DbContextCollection dbContextCollection, PostgreSqlContainer? postgresContainer, PostgreSqlContainer? postgresPrototypeContainer)
{
    public DbContextCollection DbContextCollection { get; } = dbContextCollection;
    public PostgreSqlContainer? PostgresContainer { get; } = postgresContainer;
    public PostgreSqlContainer? PostgresPrototypeContainer { get; } = postgresPrototypeContainer;
}