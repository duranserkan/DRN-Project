using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace DRN.Framework.Testing.Contexts.Postgres;

public class PostgresContextIsolated(DrnTestContext testContext)
{
    private DrnTestContext DrnTestContext { get; } = testContext;
    private ContainerContext ContainerContext => DrnTestContext.ContainerContext;

    public async Task<PostgreSqlContainer> StartAsync(PostgresContainerSettings? settings = null)
    {
        var container = PostgresContext.BuildContainer(settings);
        ContainerContext.AddContainer(container);

        await container.StartAsync();

        return container;
    }

    public async Task<PostgreSqlContainer> ApplyMigrationsAsync(PostgresContainerSettings? settings = null)
    {
        var container = await StartAsync(settings);
        var dbContexts = PostgresContext.SetConnectionStrings(DrnTestContext, container);
        await MigrateDbContextsAsync(dbContexts);

        return container;
    }

    /// <summary>
    /// Can be used for rapid prototyping for single dbContext since Database.EnsureCreatedAsync doesn't support multiple contexts on single database
    /// </summary>
    public async Task<PostgreSqlContainer> EnsureDatabaseAsync<TContext>(PostgresContainerSettings? settings = null)
        where TContext : DbContext
    {
        var container = await StartAsync(settings);
        var dbContexts = PostgresContext.SetConnectionStrings(DrnTestContext, container);
        var dbContext = Array.Find(dbContexts, d => d.GetType() == typeof(TContext));
        ArgumentNullException.ThrowIfNull(dbContext);

        await dbContext.Database.EnsureCreatedAsync();

        return container;
    }

    private static async Task MigrateDbContextsAsync(DbContext[] dbContexts)
    {
        if (dbContexts.Length == 0) return;
        
        foreach (var toBeMigratedDbContext in dbContexts) 
            await toBeMigratedDbContext.Database.MigrateAsync();
    }
}