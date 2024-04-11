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
    private static readonly object ContainerLock = new();
    private static readonly Lazy<PostgreSqlContainer> Container = new(() => BuildContainer());

    private static readonly object MigrationLock = new();
    private static readonly List<Type> MigratedDbContexts = new();

    public TestContext TestContext { get; } = testContext;
    public IsolatedPostgresContext Isolated { get; } = new(testContext);

    public PostgreSqlContainer Start()
    {
        lock (ContainerLock)
        {
            if (_started) return Container.Value;
            Container.Value.StartAsync().GetAwaiter().GetResult();
            _started = true;
            return Container.Value;
        }
    }

    public PostgreSqlContainer StartAndApplyMigrations()
    {
        var container = Start();
        var dbContexts = SetConnectionStrings(TestContext, container);

        lock (MigrationLock)
        {
            var toBeMigratedDbContextTypes = dbContexts.Select(x => x.GetType()).Except(MigratedDbContexts).ToArray();
            var migrationTasks = dbContexts.Where(dbContext => toBeMigratedDbContextTypes.Contains(dbContext.GetType()))
                .Select(dbContext => dbContext.Database.MigrateAsync()).ToArray();
            Task.WhenAll(migrationTasks).GetAwaiter().GetResult();
            MigratedDbContexts.AddRange(toBeMigratedDbContextTypes);
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
            .Where(descriptor => descriptor.ServiceType.GetCustomAttribute<HasDrnContextServiceCollectionModuleAttribute>() != null).ToArray();
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