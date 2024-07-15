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
    private static readonly SemaphoreSlim ContainerLock = new(1, 1);
    static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static readonly List<Type> MigratedDbContexts = new(10);

    public TestContext TestContext { get; } = testContext;
    public PostgresContextIsolated Isolated { get; } = new(testContext);

    public static Lazy<PostgreSqlContainer> Container { get; } = new(() => BuildContainer(PostgresContainerSettings));

    /// <summary>
    /// Update before container creation. StartAsync and ApplyMigrationsAsync methods initialize the container.
    /// Updated settings after the container initialized will not be reflected on container.
    /// </summary>
    public static PostgresContainerSettings PostgresContainerSettings { get; set; } = new();

    public static PostgreSqlContainer BuildContainer(PostgresContainerSettings? settings = null)
    {
        settings ??= new PostgresContainerSettings();

        var containerPort = PostgreSqlBuilder.PostgreSqlPort;
        var builder = new PostgreSqlBuilder().WithImage(settings.GetImageTag());
        if (settings.HasDatabase) builder = builder.WithDatabase(settings.Database);
        if (settings.HasUsername) builder = builder.WithUsername(settings.Username);
        if (settings.HasPassword) builder = builder.WithPassword(settings.Password);
        if (settings.HasValidHostPort) builder = builder.WithPortBinding(settings.HostPort, containerPort);
        if (settings.Reuse) builder = builder.WithReuse(true);

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
        var postgresContainer = BuildContainer(options.PostgresContainerSettings);
        await postgresContainer.StartAsync();

        var dbContextCollection = GetDbContextCollection(applicationBuilder.Services, postgresContainer);
        applicationBuilder.Configuration.AddObjectToJsonConfiguration(dbContextCollection.ConnectionStrings);

        return new PostgresCollection(dbContextCollection, postgresContainer);
    }
}