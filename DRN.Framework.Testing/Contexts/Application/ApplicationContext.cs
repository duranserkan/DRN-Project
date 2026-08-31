using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Web;
using LogLevel = NLog.LogLevel;

namespace DRN.Framework.Testing.Contexts.Application;

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

    internal static ITestOutputHelper? ResolveOutputHelper(ITestOutputHelper? supplied = null, bool debuggerOnly = true) =>
        ApplicationContextHelper.ResolveOutputHelper(supplied, debuggerOnly);

    public WebApplicationFactory<TEntryPoint> CreateApplication<TEntryPoint>(
        Action<IWebHostBuilder>? webHostConfigurator = null,
        params string[] additionalAddresses)
        where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new()
    {
        var outputHelper = ResolveOutputHelper();
        return CreateApplicationCore<TEntryPoint>(outputHelper, webHostConfigurator, additionalAddresses);
    }

    /// <summary>
    /// Creates an application and registers custom service names or address aliases in the in-memory router.
    /// </summary>
    public WebApplicationFactory<TEntryPoint> CreateApplicationForService<TEntryPoint>(string serviceName, params string[] additionalServiceNames)
        where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new()
    {
        string[] allAddresses = [serviceName, .. additionalServiceNames];
        return CreateApplication<TEntryPoint>(webHostConfigurator: null, additionalAddresses: allAddresses);
    }

    internal WebApplicationFactory<TEntryPoint> CreateApplicationCore<TEntryPoint>(
        ITestOutputHelper? outputHelper,
        Action<IWebHostBuilder>? webHostConfigurator = null,
        IEnumerable<string>? additionalAddresses = null)
        where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new()
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
        ApplicationContextHelper.DiscoverConfiguredAddresses(config, typeof(TEntryPoint), extraAddresses);

        RouterHandler.Register(typeof(TEntryPoint), () => factory.Server.CreateHandler(), extraAddresses);

        return factory;
    }

    internal void RegisterConfiguredAddresses(Type entryPointType, IConfiguration configuration)
    {
        var addresses = new List<string>();
        ApplicationContextHelper.DiscoverConfiguredAddresses(configuration, entryPointType, addresses);
        foreach (var address in addresses)
            RouterHandler.RegisterAddress(address, entryPointType);
    }

    /// <summary>
    /// Explicitly maps a hostname, port, or base URL to a registered application's in-memory <see cref="HttpMessageHandler"/>.
    /// </summary>
    public void MapAddress<TEntryPoint>(string address) where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new() =>
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
        params string[] additionalAddresses) where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new()
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
        params string[] additionalServiceNames) where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new()
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
        params string[] additionalAddresses) where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new()
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
        params string[] additionalServiceNames) where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new()
    {
        string[] allAddresses = [serviceName, .. additionalServiceNames];
        return await CreateClientAsync<TEntryPoint>(outputHelper: null, clientOptions: null, additionalAddresses: allAddresses);
    }

    public WebApplicationFactory<TEntryPoint>? GetCreatedApplication<TEntryPoint>() where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new()
        => _factories.TryGetValue(typeof(TEntryPoint), out var factory) && factory is WebApplicationFactory<TEntryPoint> application
            ? application
            : null;

    public IReadOnlyCollection<IDisposable> GetCreatedApplications() => _factories.Values.ToArray();

    internal bool HasCreatedApplication => _factories.Count > 0;

    internal void UseApplicationFactory<TEntryPoint>(IDisposable factory) where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new()
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
