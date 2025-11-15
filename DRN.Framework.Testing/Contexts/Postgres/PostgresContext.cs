using DRN.Framework.EntityFramework;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using ConfigurationException = DRN.Framework.SharedKernel.ConfigurationException;

namespace DRN.Framework.Testing.Contexts.Postgres;

public class PostgresContext(DrnTestContext testContext)
{
    private static readonly SemaphoreSlim ContainerLock = new(1, 1);
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static readonly List<Type> MigratedDbContexts = new(10);

    public DrnTestContext DrnTestContext { get; } = testContext;
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
        if (settings.HasContainerName) builder = builder.WithName(settings.ContainerName);

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
        var dbContexts = SetConnectionStrings(DrnTestContext, container);

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

    public static DbContext[] SetConnectionStrings(DrnTestContext testContext, PostgreSqlContainer container)
    {
        var dbContextCollection = GetDbContextCollection(testContext.ServiceCollection);
        foreach (var descriptor in dbContextCollection.ServiceDescriptors)
            dbContextCollection.ConnectionStrings.Upsert(descriptor.Key, container.GetConnectionString());

        testContext.AddToConfiguration(dbContextCollection.ConnectionStrings);

        var empty = !dbContextCollection.Any;
        if (empty) return [];

        var serviceProvider = testContext.BuildServiceProvider();
        var dbContexts = dbContextCollection.GetDbContexts(serviceProvider);

        return dbContexts;
    }


    public static DbContextCollection GetDbContextCollection(IServiceCollection serviceCollection)
    {
        var dbContextCollection = new DbContextCollection();
        var descriptors = serviceCollection.GetAllAssignableTo<DbContext>()
            .Where(descriptor => descriptor.ServiceType.GetCustomAttribute<DrnContextServiceRegistrationAttribute>() != null)
            .ToArray();

        foreach (var descriptor in descriptors)
            dbContextCollection.ServiceDescriptors[descriptor.ServiceType.Name] = descriptor;

        return dbContextCollection;
    }

    internal static async Task<PostgresCollection> LaunchPostgresAsync(WebApplicationBuilder applicationBuilder, IAppSettings appSettings, ExternalDependencyLaunchOptions options)
    {
        var dbContextCollection = GetDbContextCollection(applicationBuilder.Services);
        var dbContextTypes = dbContextCollection.ServiceDescriptors.Select(d => d.Value.ServiceType).ToArray();
        ValidatePrototypeModeUsage(dbContextTypes);

        PostgreSqlContainer? container = null;
        PostgreSqlContainer? prototypeContainer = null;
        options.PostgresContainerSettings.ContainerName = $"{appSettings.ApplicationName} DevelopDB".ToSnakeCase();
        foreach (var descriptor in dbContextCollection.ServiceDescriptors)
        {
            var optionsAttributes = DbContextConventions.GetContextAttributes(descriptor.Value.ServiceType);
            if (optionsAttributes.Any(x => x.UsePrototypeMode)) //prototype dbcontext uses separate throw away database
            {
                var prototypeContainerSettings = options.PostgresContainerSettings.Clone(options.PostgresContainerSettings.HostPort + 1);
                prototypeContainerSettings.ContainerName = $"{prototypeContainerSettings.ContainerName} {descriptor.Key} Prototype".ToSnakeCase();

                prototypeContainer = BuildContainer(prototypeContainerSettings);
                await prototypeContainer.StartAsync();

                dbContextCollection.ConnectionStrings.Upsert(descriptor.Key, prototypeContainer.GetConnectionString());
                continue;
            }

            if (container == null)
            {
                container = BuildContainer(options.PostgresContainerSettings); //development environment dbcontexts use shared database
                await container.StartAsync();
            }

            dbContextCollection.ConnectionStrings.Upsert(descriptor.Key, container.GetConnectionString());
        }

        applicationBuilder.Configuration.AddObjectToJsonConfiguration(dbContextCollection.ConnectionStrings);

        return new PostgresCollection(dbContextCollection, container, prototypeContainer);
    }

    private static void ValidatePrototypeModeUsage(Type[] dbContextTypes)
    {
        var typeAttributeDictionary = DbContextConventions.InitializeAll(dbContextTypes);
        var prototypeDbContextTypes = typeAttributeDictionary
            .Where(pair => pair.Value.Any(attribute => attribute.UsePrototypeMode))
            .Select(pair => pair.Key).ToArray();

        if (prototypeDbContextTypes.Length > 1)
        {
            var prototypeContextNames = string.Join(' ', prototypeDbContextTypes.Select(t => t.Name));
            throw new ConfigurationException($"PrototypeModeUse count cannot be more than 1. Following DbContexts' use PrototypeMode: {prototypeContextNames}");
        }
    }
}