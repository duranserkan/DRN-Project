using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Web;
using LogLevel = NLog.LogLevel;

namespace DRN.Framework.Testing.Contexts;

public sealed class ApplicationContext(DrnTestContext testContext) : IDisposable
{
    private readonly Dictionary<Type, IDisposable> _factories = [];
    private ServiceDescriptor[]? _initialServiceDescriptors;

    /// <summary>
    /// In-memory router handler that dispatches HTTP requests across hosted applications.
    /// Owned and disposed by <see cref="ApplicationContext"/>. When creating custom <see cref="HttpClient"/>
    /// instances wrapping this handler directly, specify <c>disposeHandler: false</c> to prevent premature router disposal.
    /// </summary>
    public ApplicationContextRouterHandler RouterHandler { get; } = new();

    internal static ITestOutputHelper? ResolveOutputHelper(ITestOutputHelper? supplied = null, bool debuggerOnly = true)
    {
        if (debuggerOnly && !Debugger.IsAttached)
            return null;

        return supplied ?? TestContext.Current.TestOutputHelper;
    }

    public WebApplicationFactory<TEntryPoint> CreateApplication<TEntryPoint>(
        Action<IWebHostBuilder>? webHostConfigurator = null,
        params string[] additionalAddresses)
        where TEntryPoint : class
    {
        var outputHelper = ResolveOutputHelper();
        return CreateApplicationCore<TEntryPoint>(outputHelper, webHostConfigurator, additionalAddresses);
    }

    /// <summary>
    /// Creates an application and registers custom service names or address aliases in the in-memory router.
    /// </summary>
    public WebApplicationFactory<TEntryPoint> CreateApplicationForService<TEntryPoint>(string serviceName, params string[] additionalServiceNames)
        where TEntryPoint : class
    {
        string[] allAddresses = [serviceName, .. additionalServiceNames];
        return CreateApplication<TEntryPoint>(webHostConfigurator: null, additionalAddresses: allAddresses);
    }

    internal WebApplicationFactory<TEntryPoint> CreateApplicationCore<TEntryPoint>(
        ITestOutputHelper? outputHelper,
        Action<IWebHostBuilder>? webHostConfigurator = null,
        IEnumerable<string>? additionalAddresses = null)
        where TEntryPoint : class
    {
        DisposeFactory(typeof(TEntryPoint));

        _initialServiceDescriptors ??= testContext.ServiceCollection.ToArray();
        var initialServiceDescriptors = _initialServiceDescriptors;

        // Add program services to drnTestContext
        using (var tempApplicationFactory = new DrnWebApplicationFactory<TEntryPoint>(testContext, true, webHostBuilder =>
               {
                   // only need service collection descriptors, so ValidateServicesAddedByAttributes should not fail test at this stage
                   var configuration = testContext.BuildConfigurationRoot();
                   webHostBuilder.UseConfiguration(configuration);
                   webHostBuilder.UseSetting(DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.SkipValidation)), "true");
                   webHostBuilder.UseSetting(DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.TemporaryApplication)), "true");
                   webHostBuilder.ConfigureLogging(logging => logging.ClearProviders());

                   webHostConfigurator?.Invoke(webHostBuilder);
                   webHostBuilder.ConfigureServices(services => testContext.ServiceCollection.Add(services));
               }))
        {
            _ = tempApplicationFactory.Server; // To trigger webHostBuilder action
        }

        // register action to pass test context configuration to web application.
        // This will be triggered when TestServer or HttpClient requested until then further configurations can be added to test context configuration
        IConfiguration? factoryConfiguration = null;
        var factory = new DrnWebApplicationFactory<TEntryPoint>(testContext, false, webHostBuilder =>
        {
            // Derived factories replay this configurator after the active provider changes to the parent host.
            var configuration = factoryConfiguration ??= testContext.BuildConfigurationRoot();
            webHostBuilder.UseConfiguration(configuration);
            webHostBuilder.ConfigureServices(services =>
            {
                services.Add(initialServiceDescriptors);
                services.AddSingleton<HttpMessageHandler>(_ => RouterHandler.CreateForwardingHandler());
                services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(builder => { builder.PrimaryHandler = RouterHandler.CreateForwardingHandler(); });
                });
            });

            webHostBuilder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                if (outputHelper == null)
                    return;

                var testMethod = testContext.MethodContext.TestMethod;
                var testName = testMethod.DeclaringType != null
                    ? $"{testMethod.DeclaringType.Name}.{testMethod.Name}"
                    : testMethod.Name;

                // Create a custom NLog target that writes to the test output helper
                var testOutputTarget = new TestOutputTarget(outputHelper, testName);
                var config = new LoggingConfiguration();
                config.AddTarget(testOutputTarget);
                config.AddRule(LogLevel.Info, LogLevel.Fatal, testOutputTarget);

                var logFactory = new LogFactory();
                logFactory.Configuration = config;

                var options = new NLogAspNetCoreOptions()
                {
                    ReplaceLoggerFactory = false,
                    RemoveLoggerFactoryFilter = false
                };
                logging.AddNLogWeb(logFactory, options);
            });

            webHostConfigurator?.Invoke(webHostBuilder);
            webHostBuilder.ConfigureServices(services =>
            {
                testContext.OverrideServiceCollection(services);
                testContext.MethodContext.ReplaceSubstitutedInterfaces(services);
                testContext.ServiceCollection = new ServiceCollection { services };
            });
        });

        _factories[typeof(TEntryPoint)] = factory;

        var extraAddresses = new List<string>(additionalAddresses ?? []);
        var config = testContext.BuildConfigurationRoot();
        DiscoverConfiguredAddresses(config, typeof(TEntryPoint), extraAddresses);

        RouterHandler.Register(typeof(TEntryPoint), () => factory.Server.CreateHandler(), extraAddresses);

        return factory;
    }

    internal void RegisterConfiguredAddresses(Type entryPointType, IConfiguration configuration)
    {
        var addresses = new List<string>();
        DiscoverConfiguredAddresses(configuration, entryPointType, addresses);
        foreach (var address in addresses)
            RouterHandler.RegisterAddress(address, entryPointType);
    }

    private static readonly string[] AddressSuffixes = ["Address", "Url", "Uri"];

    private static void DiscoverConfiguredAddresses(IConfiguration configuration, Type entryPointType, List<string> addresses)
    {
        var typeName = entryPointType.Name;
        var shortName = GetShortName(typeName);

        var kestrelEndpoints = configuration.GetSection("Kestrel:Endpoints");
        if (kestrelEndpoints.Exists())
        {
            foreach (var endpoint in kestrelEndpoints.GetChildren())
            {
                var url = endpoint["Url"];
                if (!string.IsNullOrWhiteSpace(url))
                    addresses.Add(url);
            }
        }

        foreach (var kvp in configuration.AsEnumerable())
        {
            if (string.IsNullOrWhiteSpace(kvp.Value))
                continue;

            var key = kvp.Key;
            var isAddressKey = key.EndsWith("Address", StringComparison.OrdinalIgnoreCase) ||
                               key.EndsWith("Url", StringComparison.OrdinalIgnoreCase) ||
                               key.EndsWith("Uri", StringComparison.OrdinalIgnoreCase);

            if (!isAddressKey)
                continue;

            var segments = key.Split(':');
            if (MatchesAddressKey(segments, typeName, shortName))
                addresses.Add(kvp.Value);
        }
    }

    private static bool MatchesAddressKey(string[] segments, string typeName, string shortName)
    {
        if (segments.Length == 0)
            return false;

        var leaf = segments[^1];
        if (MatchesAddressSegment(leaf, typeName, shortName))
            return true;

        if (segments.Length > 1 && AddressSuffixes.Any(suffix => leaf.Equals(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            var parent = segments[^2];
            return IsExactMatch(parent, typeName, shortName);
        }

        return false;
    }

    private static bool MatchesAddressSegment(string segment, string typeName, string shortName)
    {
        foreach (var suffix in AddressSuffixes)
        {
            if (segment.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && segment.Length > suffix.Length)
            {
                var prefix = segment[..^suffix.Length];
                if (IsExactMatch(prefix, typeName, shortName))
                    return true;
            }
        }

        return false;
    }

    private static bool IsExactMatch(string value, string typeName, string shortName) =>
        value.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrEmpty(shortName) && value.Equals(shortName, StringComparison.OrdinalIgnoreCase));

    private static string GetShortName(string typeName) => ApplicationContextRouterHandler.GetShortName(typeName);

    /// <summary>
    /// Explicitly maps a hostname, port, or base URL to a registered application's in-memory <see cref="HttpMessageHandler"/>.
    /// </summary>
    public void MapAddress<TEntryPoint>(string address) where TEntryPoint : class =>
        RouterHandler.RegisterAddress(address, typeof(TEntryPoint));

    /// <summary>
    /// Explicitly maps a hostname, port, or base URL to a specific <see cref="HttpMessageHandler"/>.
    /// </summary>
    public void MapAddress(string address, HttpMessageHandler handler) =>
        RouterHandler.RegisterAddress(address, handler);

    /// <summary>
    /// Most used defaults and bindings for testing an api endpoint gathered together with custom address bindings.
    /// </summary>
    public async Task<WebApplicationFactory<TEntryPoint>> CreateApplicationAndBindDependenciesAsync<TEntryPoint>(
        ITestOutputHelper? outputHelper = null,
        params string[] additionalAddresses) where TEntryPoint : class
    {
        var resolvedOutputHelper = ResolveOutputHelper(outputHelper);
        var application = CreateApplicationCore<TEntryPoint>(resolvedOutputHelper, additionalAddresses: additionalAddresses);
        await testContext.ContainerContext.BindExternalDependenciesAsync();
        application.Server.PreserveExecutionContext = true;

        return application;
    }

    /// <summary>
    /// Most used defaults and bindings for testing an api endpoint gathered together with custom service names.
    /// </summary>
    public async Task<WebApplicationFactory<TEntryPoint>> CreateApplicationAndBindDependenciesForServiceAsync<TEntryPoint>(
        string serviceName,
        params string[] additionalServiceNames) where TEntryPoint : class
    {
        string[] allAddresses = [serviceName, .. additionalServiceNames];
        return await CreateApplicationAndBindDependenciesAsync<TEntryPoint>(outputHelper: null, additionalAddresses: allAddresses);
    }

    /// <summary>
    /// Most used defaults and bindings for testing an api endpoint gathered together with custom address bindings.
    /// </summary>
    /// <returns>HttpClient instead of FlurlClient to prevent flurl http test server collision</returns>
    public async Task<HttpClient> CreateClientAsync<TEntryPoint>(
        ITestOutputHelper? outputHelper = null,
        WebApplicationFactoryClientOptions? clientOptions = null,
        params string[] additionalAddresses) where TEntryPoint : class
    {
        clientOptions ??= new WebApplicationFactoryClientOptions();
        clientOptions.BaseAddress = new Uri(TestEnvironment.TestContextAddress);

        var application = await CreateApplicationAndBindDependenciesAsync<TEntryPoint>(outputHelper, additionalAddresses);
        var client = application.CreateClient(clientOptions);

        return client;
    }

    /// <summary>
    /// Most used defaults and bindings for testing an api endpoint gathered together with custom service names.
    /// </summary>
    public async Task<HttpClient> CreateClientForServiceAsync<TEntryPoint>(
        string serviceName,
        params string[] additionalServiceNames) where TEntryPoint : class
    {
        string[] allAddresses = [serviceName, .. additionalServiceNames];
        return await CreateClientAsync<TEntryPoint>(outputHelper: null, clientOptions: null, additionalAddresses: allAddresses);
    }

    public WebApplicationFactory<TEntryPoint>? GetCreatedApplication<TEntryPoint>() where TEntryPoint : class
        => _factories.TryGetValue(typeof(TEntryPoint), out var factory) && factory is WebApplicationFactory<TEntryPoint> application
            ? application
            : null;

    public IReadOnlyCollection<IDisposable> GetCreatedApplications() => _factories.Values.ToArray();

    internal bool HasCreatedApplication => _factories.Count > 0;

    internal void UseApplicationFactory<TEntryPoint>(IDisposable factory) where TEntryPoint : class
        => _factories[typeof(TEntryPoint)] = factory;

    private void DisposeFactory(Type type)
    {
        if (!_factories.Remove(type, out var factory))
            return;

        RouterHandler.Unregister(type);
        try
        {
            factory.Dispose();
        }
        catch
        {
            _factories[type] = factory;
            throw;
        }
    }

    [SuppressMessage("SonarQube", "S3877", Justification = "ApplicationContext intentionally surfaces disposal exceptions to ensure test teardown failures are not silently swallowed.")]
    [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "ApplicationContext intentionally surfaces disposal exceptions to ensure test teardown failures are not silently swallowed.")]
    public void Dispose()
    {
        var factories = _factories.ToArray();
        _factories.Clear();
        RouterHandler.Clear();
        var exceptions = new List<Exception>();

        foreach (var (type, factory) in factories)
        {
            try
            {
                factory.Dispose();
            }
            catch (Exception ex)
            {
                _factories[type] = factory;
                exceptions.Add(ex);
            }
        }

        try
        {
            if (_initialServiceDescriptors != null && _factories.Count == 0)
            {
                testContext.ServiceCollection = new ServiceCollection { _initialServiceDescriptors };
                _initialServiceDescriptors = null;
            }

            testContext.ClearApplicationServiceProvider();
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
        }

        if (_factories.Count == 0 && factories.Length > 0)
        {
            try
            {
                testContext.DisposeOwnedServiceProvider();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count == 1)
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        if (exceptions.Count > 1)
            throw new AggregateException("One or more errors occurred during ApplicationContext disposal.", exceptions);
    }
}

public class DrnWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly DrnTestContext _context;
    private readonly Action<IWebHostBuilder>? _webHostConfigurator;

    public DrnWebApplicationFactory(DrnTestContext context, bool temporary = false)
        : this(context, temporary, null)
    {
    }

    internal DrnWebApplicationFactory(
        DrnTestContext context,
        bool temporary,
        Action<IWebHostBuilder>? webHostConfigurator)
    {
        _context = context;
        Temporary = temporary;
        _webHostConfigurator = webHostConfigurator;
    }

    private bool Temporary { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => _webHostConfigurator?.Invoke(builder);

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Preserve WebApplicationFactory host setup while retaining a partially built host for failure cleanup.
        var capturingBuilder = new CapturingHostBuilder(builder);
        try
        {
            var host = base.CreateHost(capturingBuilder);
            if (Temporary)
                return new FailureSafeHost(host);

            _context.UseApplicationServiceProvider(host.Services);
            if (host.Services.GetService(typeof(IConfiguration)) is IConfiguration configuration)
                _context.ApplicationContext.RegisterConfiguredAddresses(typeof(TEntryPoint), configuration);

            return new FailureSafeHost(host);
        }
        catch (Exception hostException)
        {
            var host = capturingBuilder.Host;
            if (host == null)
                throw;

            try
            {
                host.Dispose();
            }
            catch (Exception disposalException)
            {
                throw new AggregateException(hostException, disposalException);
            }

            throw;
        }
    }

    private sealed class FailureSafeHost(IHost host) : IHost
    {
        private bool _disposedAfterStopFailure;
        private bool _stopped;

        public IServiceProvider Services => host.Services;

        public Task StartAsync(CancellationToken cancellationToken = default) =>
            host.StartAsync(cancellationToken);

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_disposedAfterStopFailure || _stopped)
                return;

            _stopped = true;
            try
            {
                await host.StopAsync(cancellationToken);
            }
            catch (Exception stopException)
            {
                try
                {
                    host.Dispose();
                    _disposedAfterStopFailure = true;
                }
                catch (Exception disposalException)
                {
                    throw new AggregateException(stopException, disposalException);
                }

                throw;
            }
        }

        public void Dispose()
        {
            if (!_disposedAfterStopFailure)
                host.Dispose();
        }
    }

    private sealed class CapturingHostBuilder(IHostBuilder builder) : IHostBuilder
    {
        public IHost? Host { get; private set; }
        public IDictionary<object, object> Properties => builder.Properties;

        public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
        {
            builder.ConfigureHostConfiguration(configureDelegate);
            return this;
        }

        public IHostBuilder ConfigureAppConfiguration(
            Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
        {
            builder.ConfigureAppConfiguration(configureDelegate);
            return this;
        }

        public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
        {
            builder.ConfigureServices(configureDelegate);
            return this;
        }

        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
            IServiceProviderFactory<TContainerBuilder> factory) where TContainerBuilder : notnull
        {
            builder.UseServiceProviderFactory(factory);
            return this;
        }

        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
            Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory)
            where TContainerBuilder : notnull
        {
            builder.UseServiceProviderFactory(factory);
            return this;
        }

        public IHostBuilder ConfigureContainer<TContainerBuilder>(
            Action<HostBuilderContext, TContainerBuilder> configureDelegate)
        {
            builder.ConfigureContainer(configureDelegate);
            return this;
        }

        public IHost Build() => Host = builder.Build();
    }
}

// Custom NLog target for writing to ITestOutputHelper
public sealed class TestOutputTarget : TargetWithLayout
{
    private readonly ITestOutputHelper _testOutputHelper;

    public TestOutputTarget(ITestOutputHelper testOutputHelper, string? testName = null)
    {
        _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        Name = "testOutput";
        var testTag = !string.IsNullOrWhiteSpace(testName) ? $" :: {testName}" : string.Empty;
        Layout =
            $$"""
              [BEGIN ${date:format=HH\:mm\:ss.fffffff} ${level:format=Name:padding=-3:uppercase=true} ${logger}{{testTag}}]
              ${message}
              [END ${date:format=HH\:mm\:ss.fffffff} ${level:format=Name:padding=-3:uppercase=true} ${logger}{{testTag}}]${newline}
              """;
    }

    protected override void Write(LogEventInfo logEvent)
    {
        try
        {
            var logMessage = RenderLogEvent(Layout, logEvent);
            _testOutputHelper.WriteLine(logMessage);
        }
        catch (Exception ex)
        {
            // Avoid throwing exceptions from logging infrastructure
            // In test scenarios, we might want to output to debug instead
            Debug.WriteLine($"Failed to write to test output: {ex.Message}");
        }
    }
}
