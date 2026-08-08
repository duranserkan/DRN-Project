using System.Diagnostics;
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
    private IDisposable? _factory;
    private ServiceDescriptor[]? _initialServiceDescriptors;

    private static ITestOutputHelper? ResolveOutputHelper(
        ITestOutputHelper? supplied = null,
        bool debuggerOnly = true)
    {
        if (debuggerOnly && !Debugger.IsAttached)
            return null;

        return supplied ?? Xunit.TestContext.Current.TestOutputHelper;
    }

    public WebApplicationFactory<TEntryPoint> CreateApplication<TEntryPoint>(Action<IWebHostBuilder>? webHostConfigurator = null)
        where TEntryPoint : class
    {
        var outputHelper = ResolveOutputHelper();
        return CreateApplicationCore<TEntryPoint>(outputHelper, webHostConfigurator);
    }

    private WebApplicationFactory<TEntryPoint> CreateApplicationCore<TEntryPoint>(
        ITestOutputHelper? outputHelper,
        Action<IWebHostBuilder>? webHostConfigurator = null)
        where TEntryPoint : class
    {
        Dispose();

        _initialServiceDescriptors = testContext.ServiceCollection.ToArray();
        var initialServiceDescriptors = _initialServiceDescriptors;
        //Add program services to drnTestContext
        using (var tempApplicationFactory = new DrnWebApplicationFactory<TEntryPoint>(testContext, true, webHostBuilder =>
        {
            //only need service collection descriptors, so ValidateServicesAddedByAttributes should not fail test at this stage
            var configuration = testContext.GetRequiredService<IConfiguration>();
            webHostBuilder.UseConfiguration(configuration);
            webHostBuilder.UseSetting(DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.SkipValidation)), "true");
            webHostBuilder.UseSetting(DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.TemporaryApplication)), "true");
            webHostBuilder.ConfigureLogging(logging => logging.ClearProviders());

            webHostConfigurator?.Invoke(webHostBuilder);
            webHostBuilder.ConfigureServices(services => testContext.ServiceCollection.Add(services));
        }))
        {
            _ = tempApplicationFactory.Server; //To trigger webHostBuilder action
        }

        //register action to pass test context configuration to web application.
        //This will be triggered when TestServer or HttpClient requested until then further configurations can be added to test context configuration
        IConfiguration? factoryConfiguration = null;
        var factory = new DrnWebApplicationFactory<TEntryPoint>(testContext, false, webHostBuilder =>
        {
            // Derived factories replay this configurator after the active provider changes to the parent host.
            var configuration = factoryConfiguration ??= testContext.GetRequiredService<IConfiguration>();
            webHostBuilder.UseConfiguration(configuration);
            webHostBuilder.ConfigureServices(services => services.Add(initialServiceDescriptors));

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

        UseApplicationFactory(factory);

        return factory;
    }

    /// <summary>
    /// Most used defaults and bindings for testing an api endpoint gathered together
    /// </summary>
    public async Task<WebApplicationFactory<TEntryPoint>> CreateApplicationAndBindDependenciesAsync<TEntryPoint>(
        ITestOutputHelper? outputHelper = null) where TEntryPoint : class
    {
        var resolvedOutputHelper = ResolveOutputHelper(outputHelper);
        var application = CreateApplicationCore<TEntryPoint>(resolvedOutputHelper);
        await testContext.ContainerContext.BindExternalDependenciesAsync();
        application.Server.PreserveExecutionContext = true;

        return application;
    }

    /// <summary>
    /// Most used defaults and bindings for testing an api endpoint gathered together
    /// </summary>
    /// <returns>HttpClient instead of FlurlClient to prevent flurl http test server collision</returns>
    public async Task<HttpClient> CreateClientAsync<TEntryPoint>(ITestOutputHelper? outputHelper = null,
        WebApplicationFactoryClientOptions? clientOptions = null) where TEntryPoint : class
    {
        clientOptions ??= new WebApplicationFactoryClientOptions();
        clientOptions.BaseAddress = new Uri(TestEnvironment.TestContextAddress);

        var application = await CreateApplicationAndBindDependenciesAsync<TEntryPoint>(outputHelper);
        var client = application.CreateClient(clientOptions);

        return client;
    }

    public WebApplicationFactory<TEntryPoint>? GetCreatedApplication<TEntryPoint>() where TEntryPoint : class
        => (WebApplicationFactory<TEntryPoint>?)_factory;

    internal bool HasCreatedApplication => _factory != null;

    internal void UseApplicationFactory(IDisposable factory) => _factory = factory;

    public void Dispose()
    {
        var factory = _factory;
        try
        {
            factory?.Dispose();
            _factory = null;
        }
        finally
        {
            if (_initialServiceDescriptors != null)
            {
                testContext.ServiceCollection = new ServiceCollection { _initialServiceDescriptors };
                _initialServiceDescriptors = null;
            }
            testContext.ClearApplicationServiceProvider();
        }

        if (factory != null)
            testContext.DisposeOwnedServiceProvider();
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
            if (!Temporary)
                _context.UseApplicationServiceProvider(host.Services);

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
