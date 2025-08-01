using DRN.Framework.Testing.Providers;
using DRN.Framework.Utils;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Testing.Contexts;

public class UnitTestContext : IDisposable, IKeyedServiceProvider
{
    private readonly List<IConfigurationSource> _configurationSources = [];
    private ServiceProvider? _serviceProvider;
    private bool _disposed;

    public UnitTestContext(MethodInfo testMethod)
    {
        MethodContext = new MethodContext(testMethod);
        AddToConfiguration(nameof(DrnAppFeatures), nameof(DrnAppFeatures.ApplicationStartedBy), MethodContext.TestMethod.Name);
    }

    public MethodContext MethodContext { get; }
    public ServiceCollection ServiceCollection { get; internal set; } = [];

    /// <summary>
    /// Creates a service provider from test context service collection
    /// Init this within test method to capture name of the test
    /// It includes logging and IAppSettings by default.
    /// More default services will be added by default as the drn framework develops
    /// </summary>
    public ServiceProvider BuildServiceProvider(string appSettingsName = SettingsProvider.ConventionSettingsName)
    {
        //dispose previously initiated sp to create new
        DisposeServiceProvider();
        MethodContext.ReplaceSubstitutedInterfaces(ServiceCollection);

        var configuration = BuildConfigurationRoot(appSettingsName);
        _serviceProvider = ServiceCollection
            .AddSingleton<IConfiguration>(_ => configuration)
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

    public void ValidateServices(Func<LifetimeAttribute, bool>? ignore = null) => this.ValidateServicesAddedByAttributes(ignore: ignore);

    public IConfigurationRoot BuildConfigurationRoot(string appSettingsName = SettingsProvider.ConventionSettingsName)
    {
        var configuration = SettingsProvider.GetConfiguration(appSettingsName, MethodContext.GetTestFolderLocation(),
            _configurationSources, ServiceCollection);
        var configurationRoot = (IConfigurationRoot)configuration;

        return configurationRoot;
    }

    /// <summary>
    /// It can be used to verify selected app settings file.
    /// Make sure the settings file is copied to output directory.
    /// </summary>
    public DataProviderResultDataPath GetSettingsPath(string appSettingsName = SettingsProvider.ConventionSettingsName) =>
        SettingsProvider.GetSettingsPath(appSettingsName, MethodContext.GetTestFolderLocation());

    /// <summary>
    /// It can be used to verify selected app settings file.
    /// Make sure the settings file is copied to output directory.
    /// </summary>
    public DataProviderResult GetSettingsData(string appSettingsName = SettingsProvider.ConventionSettingsName) =>
        SettingsProvider.GetSettingsData(appSettingsName, MethodContext.GetTestFolderLocation());

    public DataProviderResult GetData(string pathRelativeToDataFolder, string? conventionDirectory = null) =>
        DataProvider.Get(pathRelativeToDataFolder, MethodContext.GetTestFolderLocation(), conventionDirectory);

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

    public override string ToString() => "TestContext";

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void DisposeServiceProvider()
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            DisposeServiceProvider();
            if (!ServiceCollection.IsReadOnly) ServiceCollection = [];
        }

        _disposed = true;
    }
}