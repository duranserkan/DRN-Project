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
    private ITestOutputHelper? _outputHelper;

    /// <summary>
    /// By default, logs are written to test output when debugger is attached in order to not leak sensitive data.
    /// Use test output logger cautiously.
    /// </summary>
    public void LogToTestOutput(ITestOutputHelper outputHelper, bool debuggerOnly = true)
    {
        if (debuggerOnly && !Debugger.IsAttached) return;

        _outputHelper = outputHelper;
    }

    public WebApplicationFactory<TEntryPoint> CreateApplication<TEntryPoint>(Action<IWebHostBuilder>? webHostConfigurator = null)
        where TEntryPoint : class
    {
        Dispose();

        var initialDrnTestContextServiceDescriptors = testContext.ServiceCollection.ToArray();
        //Add program services to drnTestContext
        var tempApplicationFactory = new DrnWebApplicationFactory<TEntryPoint>(testContext, true).WithWebHostBuilder(webHostBuilder =>
        {
            //only need service collection descriptors, so ValidateServicesAddedByAttributes should not fail test at this stage
            var configuration = testContext.GetRequiredService<IConfiguration>();
            webHostBuilder.UseConfiguration(configuration);
            webHostBuilder.UseSetting(DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.SkipValidation)), "true");
            webHostBuilder.UseSetting(DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.TemporaryApplication)), "true");

            webHostBuilder.ConfigureServices(services => testContext.ServiceCollection.Add(services));
            webHostConfigurator?.Invoke(webHostBuilder);
        });
        _ = tempApplicationFactory.Server; //To trigger webHostBuilder action
        tempApplicationFactory.Dispose();

        //register action to pass test context configuration to web application.
        //This will be triggered when TestServer or HttpClient requested until then further configurations can be added to test context configuration
        var factory = new DrnWebApplicationFactory<TEntryPoint>(testContext).WithWebHostBuilder(webHostBuilder =>
        {
            webHostBuilder.ConfigureServices(services =>
            {
                services.Add(initialDrnTestContextServiceDescriptors);
                testContext.OverrideServiceCollection(services);
                testContext.MethodContext.ReplaceSubstitutedInterfaces(services);
                testContext.ServiceCollection = new ServiceCollection { services };
            });

            var configuration = testContext.GetRequiredService<IConfiguration>();
            webHostBuilder.UseConfiguration(configuration);
            webHostBuilder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                if (_outputHelper == null)
                    return;

                // Create a custom NLog target that writes to the test output helper
                var testOutputTarget = new TestOutputTarget(_outputHelper);
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
        });

        _factory = factory;

        return factory;
    }

    /// <summary>
    /// Most used defaults and bindings for testing an api endpoint gathered together
    /// </summary>
    public async Task<WebApplicationFactory<TEntryPoint>> CreateApplicationAndBindDependenciesAsync<TEntryPoint>(
        ITestOutputHelper? outputHelper = null) where TEntryPoint : class
    {
        if (outputHelper != null)
            LogToTestOutput(outputHelper);

        var application = CreateApplication<TEntryPoint>();
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
        var application = await CreateApplicationAndBindDependenciesAsync<TEntryPoint>(outputHelper);
        var client = application.CreateClient(clientOptions ?? new WebApplicationFactoryClientOptions());

        return client;
    }

    public WebApplicationFactory<TEntryPoint>? GetCreatedApplication<TEntryPoint>() where TEntryPoint : class
        => (WebApplicationFactory<TEntryPoint>?)_factory;

    public void Dispose() => _factory?.Dispose();
}

public class DrnWebApplicationFactory<TEntryPoint>(DrnTestContext context, bool temporary = false) : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    public bool Temporary { get; } = temporary;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        if (!Temporary)
            context.OverrideServiceProvider(host.Services);

        return host;
    }
}

// Custom NLog target for writing to ITestOutputHelper
public sealed class TestOutputTarget : TargetWithLayout
{
    private readonly ITestOutputHelper _testOutputHelper;

    public TestOutputTarget(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        Name = "testOutput";
        Layout = "[BEGIN ${date:format=HH\\:mm\\:ss.fffffff} ${level:format=Name:padding=-3:uppercase=true} ${logger}]${newline}${message}${newline}[END ${date:format=HH\\:mm\\:ss.fffffff} ${level:format=Name:padding=-3:uppercase=true} ${logger}]${newline}";
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