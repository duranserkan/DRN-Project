using DRN.Framework.SharedKernel.Conventions;
using DRN.Framework.Testing.Providers;
using DRN.Framework.Utils;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Testing.Contexts;

/// <summary>
/// Test context that contains a slim Service Collection so that you can add your dependencies and build a service provider.
/// It disposes itself automatically at the end of the test.
/// </summary>
public sealed class TestContext : IDisposable, IKeyedServiceProvider
{
    public static bool IsRunning { get; private set; }

    private readonly List<IConfigurationSource> _configurationSources = [];
    private ServiceProvider? _serviceProvider;

    /// <summary>
    /// Test context that contains a slim Service Collection so that you can add your dependencies and build a service provider.
    /// It disposes itself automatically at the end of the test.
    /// </summary>
    public TestContext(MethodInfo testMethod)
    {
        IsRunning = true;
        MethodContext = new(testMethod);
        ContainerContext = new ContainerContext(this);
        ApplicationContext = new ApplicationContext(this);
        _ = JsonConventions.DefaultOptions; // to trigger static ctor that replaces .net defaults with better
    }

    public MethodContext MethodContext { get; }
    public ContainerContext ContainerContext { get; }
    public ApplicationContext ApplicationContext { get; }
    public ServiceCollection ServiceCollection { get; private set; } = [];

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
        MethodContext.ReplaceSubstitutedInterfaces(ServiceCollection);

        var configuration = BuildConfigurationRoot(appSettingsName);
        _serviceProvider = ServiceCollection
            .AddSingleton<IConfiguration>(x => configuration)
            .AddLogging(logging => { logging.ClearProviders(); })
            .AddDrnUtils()
            .BuildServiceProvider(false);

        return _serviceProvider;
    }

    internal void OverrideServiceProvider(IServiceProvider serviceProvider)
    {
        if (serviceProvider is ServiceProvider sp)
            _serviceProvider = sp;
    }

    internal void OverrideServiceCollection(IServiceCollection serviceCollection)
    {
        if (serviceCollection is ServiceCollection sc)
            ServiceCollection = sc;
    }

    public IConfigurationRoot BuildConfigurationRoot(string appSettingsName = "settings")
    {
        var configuration = SettingsProvider.GetConfiguration(appSettingsName, MethodContext.GetTestFolderLocation(),
            _configurationSources, ServiceCollection);
        var configurationRoot = (IConfigurationRoot)configuration;

        return configurationRoot;
    }

    public void ValidateServices() => this.ValidateServicesAddedByAttributes();

    public string GetData(string pathRelativeToDataFolder) => DataProvider.Get(pathRelativeToDataFolder, MethodContext.GetTestFolderLocation());

    public ConfigurationDebugViewSummary GetConfigurationDebugView()
    {
        var configurationRoot = BuildConfigurationRoot();
        var debugView = new ConfigurationDebugView(new AppSettings(configurationRoot));
        return new ConfigurationDebugViewSummary(debugView);
    }

    public void AddToConfiguration(object toBeSerialized)
        => _configurationSources.Add(new ObjectToJsonConfigurationSource(toBeSerialized));

    public void AddToConfiguration(IConfigurationSource source) => _configurationSources.Add(source);

    public void AddToConfiguration(string sectionKey, string key, string value) => AddToConfiguration($"{sectionKey}:{key}", value);

    public void AddToConfiguration(string key, string value)
    {
        var configurationSource = new MemoryConfigurationSource
        {
            InitialData = new[]
            {
                new KeyValuePair<string, string?>(key, value)
            }
        };
        AddToConfiguration(configurationSource);
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

    public void Dispose()
    {
        DisposeServiceProvider();
        if (!ServiceCollection.IsReadOnly) ServiceCollection = [];
        ContainerContext.Dispose();
        ApplicationContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private void DisposeServiceProvider()
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }

    public override string ToString() => "TestContext";
}