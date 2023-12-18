using DRN.Framework.Testing.Providers;
using DRN.Framework.Utils;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.DependencyInjection;
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
    private List<IConfigurationSource> ConfigurationSources { get; } = [];
    private ServiceProvider? ServiceProvider { get; set; }
    private IConfigurationRoot ConfigurationRoot { get; set; } = null!;

    public MethodContext MethodContext { get; } = new(testMethod);
    public ServiceCollection ServiceCollection { get; } = [];

    //Todo: live template, test containers

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
        ServiceProvider = ServiceCollection
            .AddSingleton<IConfiguration>(x => configuration)
            .AddLogging(logging => { logging.ClearProviders(); })
            .AddDrnUtils()
            .BuildServiceProvider(false);

        return ServiceProvider;
    }

    public IConfigurationRoot BuildConfigurationRoot(string appSettingsName = "settings")
    {
        var configuration = SettingsProvider.GetConfiguration(appSettingsName, MethodContext.GetTestFolderLocation(), ConfigurationSources);
        ConfigurationRoot = (IConfigurationRoot)configuration;

        return ConfigurationRoot;
    }

    public void ValidateServices() => this.ValidateServicesAddedByAttributes();

    public string GetData(string pathRelativeToDataFolder) => DataProvider.Get(pathRelativeToDataFolder, MethodContext.GetTestFolderLocation());

    public string GetConfigurationDebugView()
    {
        ServiceProvider ??= BuildServiceProvider();
        return ConfigurationRoot.GetDebugView();
    }

    public void AddToConfiguration(object toBeSerialized)
    {
        ConfigurationSources.Add(new JsonSerializerConfigurationSource(toBeSerialized));
    }

    public PostgreSqlContainer AddPostgreSQLDb(string? database = null, string? username = null, string? password = null)
    {
        var builder = new PostgreSqlBuilder().WithImage("postgres:16.1");
        if (database != null) builder.WithDatabase(database);
        if (username != null) builder.WithUsername(username);
        if (password != null) builder.WithPassword(password);

        var postgreSqlContainer = builder.Build();
        postgreSqlContainer.StartAsync().Wait();

        return postgreSqlContainer;
    }

    public object? GetService(Type serviceType)
    {
        ServiceProvider ??= BuildServiceProvider();
        return ServiceProvider.GetService(serviceType);
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        ServiceProvider ??= BuildServiceProvider();
        return ServiceProvider.GetKeyedService(serviceType, serviceKey);
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        ServiceProvider ??= BuildServiceProvider();
        return ServiceProvider.GetRequiredKeyedService(serviceType, serviceKey);
    }

    public override string ToString() => "context";

    public void Dispose()
    {
        DisposeServiceProvider();
        ServiceCollection.Clear();
        GC.SuppressFinalize(this);
    }

    private void DisposeServiceProvider()
    {
        ServiceProvider?.Dispose();
        ServiceProvider = null;
    }
}