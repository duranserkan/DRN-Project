using DotNet.Testcontainers.Containers;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.Testing.Providers;
using DRN.Framework.Utils;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace DRN.Framework.Testing.Contexts;

/// <summary>
/// Test context that contains a slim Service Collection so that you can add your dependencies and build a service provider.
/// It disposes itself automatically at the end of the test.
/// </summary>
public sealed class TestContext(MethodInfo testMethod) : IDisposable, IKeyedServiceProvider
{
    private readonly List<DockerContainer> _containers = [];
    private readonly List<IConfigurationSource> _configurationSources = [];
    private ServiceProvider? _serviceProvider;

    public MethodContext MethodContext { get; } = new(testMethod);
    public ServiceCollection ServiceCollection { get; } = [];

    //Todo: live template

    /// <summary>
    /// Creates a service provider from test context service collection
    /// Init this within test method to capture name of the test
    /// It includes logging and IAppSettings by default.
    /// More default services will be added by default as the drn framework develops
    /// </summary>
    public ServiceProvider BuildServiceProvider(string appSettingsName = "settings")
    {
        //dispose previously initiated sp to create new
        DisposeServiceProvider();
        MethodContext.ReplaceSubstitutedInterfaces(this);

        var configuration = BuildConfigurationRoot(appSettingsName);
        _serviceProvider = ServiceCollection
            .AddSingleton<IConfiguration>(x => configuration)
            .AddLogging(logging => { logging.ClearProviders(); })
            .AddDrnUtils()
            .BuildServiceProvider(false);

        return _serviceProvider;
    }

    public IConfigurationRoot BuildConfigurationRoot(string appSettingsName = "settings")
    {
        var configuration = SettingsProvider.GetConfiguration(appSettingsName, MethodContext.GetTestFolderLocation(), _configurationSources);
        var configurationRoot = (IConfigurationRoot)configuration;

        return configurationRoot;
    }

    public void ValidateServices() => this.ValidateServicesAddedByAttributes();

    public string GetData(string pathRelativeToDataFolder) => DataProvider.Get(pathRelativeToDataFolder, MethodContext.GetTestFolderLocation());

    public string GetConfigurationDebugView()
    {
        var configurationRoot = BuildConfigurationRoot();
        return configurationRoot.GetDebugView();
    }

    public void AddToConfiguration(object toBeSerialized)
    {
        _configurationSources.Add(new JsonSerializerConfigurationSource(toBeSerialized));
    }

    public async Task<PostgreSqlContainer> StartPostgresAsync(string? database = null, string? username = null, string? password = null)
    {
        var builder = new PostgreSqlBuilder().WithImage("postgres:16.1");
        if (database != null) builder.WithDatabase(database);
        if (username != null) builder.WithUsername(username);
        if (password != null) builder.WithPassword(password);

        var container = builder.Build();
        _containers.Add(container);
        await container.StartAsync();

        var descriptors = ServiceCollection.GetAllAssignableTo<DbContext>()
            .Where(descriptor => descriptor.ServiceType.GetCustomAttribute<HasDRNContextServiceCollectionModuleAttribute>() != null).ToArray();
        var stringsCollection = new ConnectionStringsCollection();
        foreach (var descriptor in descriptors)
        {
            stringsCollection.Upsert(descriptor.ServiceType.Name, container.GetConnectionString());
            AddToConfiguration(stringsCollection);
        }

        if (descriptors.Length == 0) return container;

        var serviceProvider = BuildServiceProvider();
        var migrationTasks = descriptors
            .Select(d => (DbContext)serviceProvider.GetRequiredService(d.ServiceType))
            .Select(c => c.Database.MigrateAsync()).ToArray();
        await Task.WhenAll(migrationTasks);

        return container;
    }

    public object? GetService(Type serviceType)
    {
        _serviceProvider ??= BuildServiceProvider();
        return _serviceProvider.GetService(serviceType);
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        _serviceProvider ??= BuildServiceProvider();
        return _serviceProvider.GetKeyedService(serviceType, serviceKey);
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        _serviceProvider ??= BuildServiceProvider();
        return _serviceProvider.GetRequiredKeyedService(serviceType, serviceKey);
    }

    public override string ToString() => "context";

    public void Dispose()
    {
        DisposeServiceProvider();
        ServiceCollection.Clear();
        GC.SuppressFinalize(this);
        Task.WaitAll(_containers.Select(c => c.DisposeAsync().AsTask()).ToArray());
    }

    private void DisposeServiceProvider()
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }
}