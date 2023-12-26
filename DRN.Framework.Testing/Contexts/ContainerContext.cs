using DotNet.Testcontainers.Containers;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace DRN.Framework.Testing.Contexts;

public sealed class ContainerContext(TestContext testContext) : IDisposable
{
    private readonly List<DockerContainer> _containers = [];

    /// <summary>
    /// Intentionally made public to allow extension methods to support more containers
    /// </summary>
    public TestContext TestContext { get; } = testContext;

    /// <summary>
    /// Intentionally made public to allow extension methods to support more containers
    /// </summary>
    public void AddContainer(RabbitMqContainer container)
    {
        _containers.Add(container);
    }

    public async Task<RabbitMqContainer> StartRabbitMqAsync(string? version = null, string? username = null, string? password = null)
    {
        version ??= "3.12.10-alpine";
        var builder = new RabbitMqBuilder().WithImage($"rabbitmq:{version}");
        if (username != null) builder.WithUsername(username);
        if (password != null) builder.WithPassword(password);

        var container = builder.Build();
        AddContainer(container);

        await container.StartAsync();

        return container;
    }

    public async Task<PostgreSqlContainer> StartPostgresAsync(string? version = null, string? database = null, string? username = null, string? password = null)
    {
        version ??= "16.1-alpine3.19";
        var builder = new PostgreSqlBuilder().WithImage($"postgres:{version}");
        if (database != null) builder.WithDatabase(database);
        if (username != null) builder.WithUsername(username);
        if (password != null) builder.WithPassword(password);
        var container = builder.Build();
        _containers.Add(container);

        await container.StartAsync();

        return container;
    }

    public async Task<PostgreSqlContainer> StartPostgresAndApplyMigrationsAsync(
        string? version = null, string? database = null, string? username = null, string? password = null)
    {
        var container = await StartPostgresAsync(version, database, username, password);
        var dbContexts = SetPostgresConnectionStrings(container);
        await MigrateDbContextsAsync(dbContexts);

        return container;
    }

    /// <summary>
    /// Can be used for rapid prototyping for single dbContext since Database.EnsureCreatedAsync doesn't support multiple contexts on single database
    /// </summary>
    public async Task<PostgreSqlContainer> StartPostgresAndEnsureDatabaseAsync<TContext>
        (string? version = null, string? database = null, string? username = null, string? password = null) where TContext : DbContext
    {
        var container = await StartPostgresAsync(version, database, username, password);
        var dbContexts = SetPostgresConnectionStrings(container);
        var dbContext = dbContexts.FirstOrDefault(d => d.GetType() == typeof(TContext));
        ArgumentNullException.ThrowIfNull(dbContext);

        await dbContext.Database.EnsureCreatedAsync();

        return container;
    }

    private DbContext[] SetPostgresConnectionStrings(PostgreSqlContainer container)
    {
        var descriptors = TestContext.ServiceCollection.GetAllAssignableTo<DbContext>()
            .Where(descriptor => descriptor.ServiceType.GetCustomAttribute<HasDrnContextServiceCollectionModuleAttribute>() != null).ToArray();
        var stringsCollection = new ConnectionStringsCollection();
        foreach (var descriptor in descriptors)
        {
            stringsCollection.Upsert(descriptor.ServiceType.Name, container.GetConnectionString());
            TestContext.AddToConfiguration(stringsCollection);
        }

        if (descriptors.Length == 0) return [];

        var serviceProvider = TestContext.BuildServiceProvider();
        var dbContexts = descriptors.Select(d => (DbContext)serviceProvider.GetRequiredService(d.ServiceType)).ToArray();

        return dbContexts;
    }

    private static async Task MigrateDbContextsAsync(DbContext[] dbContexts)
    {
        if (dbContexts.Length == 0) return;

        var migrationTasks = dbContexts.Select(c => c.Database.MigrateAsync()).ToArray();
        await Task.WhenAll(migrationTasks);
    }

    public void Dispose()
    {
        Task.WaitAll(_containers.Select(c => c.DisposeAsync().AsTask()).ToArray());
    }
}