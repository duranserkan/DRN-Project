using DRN.Framework.EntityFramework.Context;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DRN.Framework.Testing.Contexts.Postgres;

public class PostgresContext(TestContext testContext)
{
    private static bool _started;
    static readonly SemaphoreSlim ContainerLock = new(1, 1);
    private static readonly Lazy<PostgreSqlContainer> Container = new(() => BuildContainer());

    static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static readonly List<Type> MigratedDbContexts = new(10);

    public TestContext TestContext { get; } = testContext;
    public IsolatedPostgresContext Isolated { get; } = new(testContext);

    /// <summary>
    ///  This is container instance is shared and Ryuk the Resource Reaper will remove it after all tests run
    /// </summary>
    public static async Task<PostgreSqlContainer> StartAsync()
    {
        await ContainerLock.WaitAsync();
        try
        {
            if (_started) return Container.Value;
            await Container.Value.StartAsync();
            _started = true;
            return Container.Value;
        }
        finally
        {
            ContainerLock.Release();
        }
    }

    /// <summary>
    ///  This is container instance is shared and Ryuk the Resource Reaper will remove it after all tests run
    /// </summary>
    public async Task<PostgreSqlContainer> ApplyMigrationsAsync()
    {
        var container = await StartAsync();
        var dbContexts = SetConnectionStrings(TestContext, container);

        await MigrationLock.WaitAsync();
        try
        {
            var toBeMigratedDbContextTypes = dbContexts.Select(x => x.GetType()).Except(MigratedDbContexts).ToArray();
            var migrationTasks = dbContexts
                .Where(dbContext => toBeMigratedDbContextTypes.Contains(dbContext.GetType()))
                .Select(dbContext => dbContext.Database.MigrateAsync()).ToArray();
            await Task.WhenAll(migrationTasks);
            MigratedDbContexts.AddRange(toBeMigratedDbContextTypes);
        }
        finally
        {
            MigrationLock.Release();
        }

        return container;
    }

    public static PostgreSqlContainer BuildContainer(string? database = null, string? username = null, string? password = null, string? version = null)
    {
        version ??= "16.2-alpine3.19";
        var builder = new PostgreSqlBuilder().WithImage($"postgres:{version}");
        if (database != null) builder = builder.WithDatabase(database);
        if (username != null) builder = builder.WithUsername(username);
        if (password != null) builder = builder.WithPassword(password);

        var container = builder.Build();

        return container;
    }

    public static DbContext[] SetConnectionStrings(TestContext testContext, PostgreSqlContainer container)
    {
        var descriptors = testContext.ServiceCollection.GetAllAssignableTo<DbContext>()
            .Where(descriptor => descriptor.ServiceType.GetCustomAttribute<DrnContextServiceRegistrationAttribute>() != null).ToArray();
        var stringsCollection = new ConnectionStringsCollection();
        foreach (var descriptor in descriptors)
        {
            stringsCollection.Upsert(descriptor.ServiceType.Name, container.GetConnectionString());
            testContext.AddToConfiguration(stringsCollection);
        }

        if (descriptors.Length == 0) return [];
        var serviceProvider = testContext.BuildServiceProvider();
        var dbContexts = descriptors.Select(d => (DbContext)serviceProvider.GetRequiredService(d.ServiceType)).ToArray();

        return dbContexts;
    }
}