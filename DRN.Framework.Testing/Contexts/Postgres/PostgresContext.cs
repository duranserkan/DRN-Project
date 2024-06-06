using DRN.Framework.EntityFramework;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DRN.Framework.Testing.Contexts.Postgres;

public class PostgresContext(TestContext testContext)
{
    static readonly SemaphoreSlim ContainerLock = new(1, 1);
    private static readonly Lazy<PostgreSqlContainer> Container = new(() => BuildContainer());

    static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static readonly List<Type> MigratedDbContexts = new(10);

    public static string Image { get; set; } = "postgres";
    public static string Version { get; set; } = "16.3-alpine3.19";
    public TestContext TestContext { get; } = testContext;
    public PostgresContextIsolated Isolated { get; } = new(testContext);

    //todo refactor parameters
    public static PostgreSqlContainer BuildContainer(string? database = null, string? username = null, string? password = null,
        string? version = null, int hostPort = 0, bool reuse = false)
    {
        version ??= Version;
        var containerPort = PostgreSqlBuilder.PostgreSqlPort;
        var builder = new PostgreSqlBuilder().WithImage($"{Image}:{version}");
        if (database != null) builder = builder.WithDatabase(database);
        if (username != null) builder = builder.WithUsername(username);
        if (password != null) builder = builder.WithPassword(password);
        if (hostPort is >= 0 and < 65535) builder = builder.WithPortBinding(hostPort, containerPort);
        if (reuse) builder = builder.WithReuse(true);

        var container = builder.Build();

        return container;
    }

    /// <summary>
    ///  This is container instance is shared and Ryuk the Resource Reaper will remove it after all tests run
    /// </summary>
    public static async Task<PostgreSqlContainer> StartAsync()
    {
        await ContainerLock.WaitAsync();
        try
        {
            if (Container.Value.StartedTime != default) return Container.Value;
            await Container.Value.StartAsync();
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

    public static DbContext[] SetConnectionStrings(TestContext testContext, PostgreSqlContainer container)
    {
        var dbContextCollection = GetDbContextCollection(testContext.ServiceCollection, container);
        testContext.AddToConfiguration(dbContextCollection.ConnectionStrings);

        var empty = !dbContextCollection.Any;
        if (empty) return [];

        var serviceProvider = testContext.BuildServiceProvider();
        var dbContexts = dbContextCollection.GetDbContexts(serviceProvider);

        return dbContexts;
    }

    public static DbContextCollection GetDbContextCollection(IServiceCollection serviceCollection, PostgreSqlContainer container)
    {
        var dbContextCollection = new DbContextCollection();
        var descriptors = serviceCollection.GetAllAssignableTo<DbContext>()
            .Where(descriptor => descriptor.ServiceType.GetCustomAttribute<DrnContextServiceRegistrationAttribute>() != null)
            .ToArray();

        foreach (var descriptor in descriptors)
        {
            var contextName = descriptor.ServiceType.Name;
            dbContextCollection.ConnectionStrings.Upsert(contextName, container.GetConnectionString());
            dbContextCollection.ServiceDescriptors[contextName] = descriptor;
        }

        return dbContextCollection;
    }

    internal static async Task<PostgresCollection> LaunchPostgresAsync(
        WebApplicationBuilder applicationBuilder, ExternalDependencyLaunchOptions options)
    {
        var postgresContainer = BuildContainer(hostPort: options.PostgresHostPort, reuse: options.ContainerReuse);
        await postgresContainer.StartAsync();

        var dbContextCollection = GetDbContextCollection(applicationBuilder.Services, postgresContainer);
        applicationBuilder.Configuration.AddObjectToJsonConfiguration(dbContextCollection.ConnectionStrings);

        return new PostgresCollection(dbContextCollection, postgresContainer);
    }
}