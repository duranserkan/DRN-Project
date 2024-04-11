using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace DRN.Framework.Testing.Contexts.Postgres;

public class IsolatedPostgresContext(TestContext testContext)
{
    private TestContext TestContext { get; } = testContext;
    private ContainerContext ContainerContext => TestContext.ContainerContext;

    public async Task<PostgreSqlContainer> StartAsync(string? database = null, string? username = null, string? password = null, string? version = null)
    {
        var container = PostgresContext.BuildContainer(database, username, password, version);
        ContainerContext.AddContainer(container);

        await container.StartAsync();

        return container;
    }

    public async Task<PostgreSqlContainer> ApplyMigrationsAsync(string? database = null,
        string? username = null, string? password = null, string? version = null)
    {
        var container = await StartAsync(database, username, password, version);
        var dbContexts = PostgresContext.SetConnectionStrings(TestContext, container);
        await MigrateDbContextsAsync(dbContexts);

        return container;
    }

    /// <summary>
    /// Can be used for rapid prototyping for single dbContext since Database.EnsureCreatedAsync doesn't support multiple contexts on single database
    /// </summary>
    public async Task<PostgreSqlContainer> EnsureDatabaseAsync<TContext>(string? database = null,
        string? username = null, string? password = null, string? version = null) where TContext : DbContext
    {
        var container = await StartAsync(database, username, password, version);
        var dbContexts = PostgresContext.SetConnectionStrings(TestContext, container);
        var dbContext = Array.Find(dbContexts, d => d.GetType() == typeof(TContext));
        ArgumentNullException.ThrowIfNull(dbContext);

        await dbContext.Database.EnsureCreatedAsync();

        return container;
    }

    private static async Task MigrateDbContextsAsync(DbContext[] dbContexts)
    {
        if (dbContexts.Length == 0) return;

        var migrationTasks = dbContexts.Select(c => c.Database.MigrateAsync()).ToArray();
        await Task.WhenAll(migrationTasks);
    }
}