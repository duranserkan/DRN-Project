using DRN.Framework.Testing.Providers;
using DRN.Framework.Utils;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Testing.Contexts;

/// <summary>
/// Test context that contains a slim Service Collection so that you can add your dependencies and build a service provider.
/// It disposes itself automatically at the end of the test.
/// </summary>
public sealed class TestContext : IDisposable, IKeyedServiceProvider
{
    public TestContext(MethodInfo testMethod) => MethodContext = new MethodContext(testMethod);

    private List<IConfigurationSource> ConfigurationSources { get; } = new();
    private ServiceProvider? ServiceProvider { get; set; }

    public MethodContext MethodContext { get; }
    public ServiceCollection ServiceCollection { get; } = new();
    public IConfigurationRoot ConfigurationRoot { get; private set; } = null!;

    public string GetData(string pathRelativeToDataFolder) => DataProvider.Get(pathRelativeToDataFolder, MethodContext.GetTestFolderLocation());

    //Todo: dtt, snipped and live template, test containers, update test context documentation

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

        var configuration = SettingsProvider.GetConfiguration(appSettingsName, MethodContext.GetTestFolderLocation(), ConfigurationSources);
        ConfigurationRoot = (IConfigurationRoot)configuration;
        ServiceProvider = ServiceCollection
            .AddSingleton<IConfiguration>(x => configuration)
            .AddLogging(logging => { logging.ClearProviders(); })
            .AddDrnUtils()
            .BuildServiceProvider(false);

        return ServiceProvider;
    }

    public override string ToString() => "context";

    public void AddToConfiguration(object toBeSerialized)
    {
        ConfigurationSources.Add(new JsonSerializerConfigurationSource(toBeSerialized));
    }

    public string GetConfigurationDebugView()
    {
        ServiceProvider ??= BuildServiceProvider();
        return ConfigurationRoot.GetDebugView();
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

    public void ValidateServices() => this.ValidateServicesAddedByAttributes();

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