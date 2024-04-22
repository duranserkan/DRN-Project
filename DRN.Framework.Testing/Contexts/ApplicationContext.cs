using System.Diagnostics;
using DRN.Framework.Utils.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Xunit.Abstractions;

namespace DRN.Framework.Testing.Contexts;

public sealed class ApplicationContext(TestContext testContext) : IDisposable
{
    private IDisposable? _factory;
    private ITestOutputHelper? _outputHelper;

    public WebApplicationFactory<TEntryPoint> CreateApplication<TEntryPoint>(Action<IWebHostBuilder>? webHostConfigurator = null)
        where TEntryPoint : class
    {
        Dispose();

        var initialTestContextServiceDescriptors = testContext.ServiceCollection.ToArray();
        //Add program services to testContext
        var tempApplicationFactory = new DrnWebApplicationFactory<TEntryPoint>(testContext, true).WithWebHostBuilder(webHostBuilder =>
        {
            //only need service collection descriptors so ValidateServicesAddedByAttributes should not fail test at this stage
            webHostBuilder.UseSetting(DrnServiceContainer.SkipValidationKey, DrnServiceContainer.SkipValidation);
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
                services.Add(initialTestContextServiceDescriptors);
                testContext.OverrideServiceCollection(services);
                testContext.MethodContext.ReplaceSubstitutedInterfaces(services);
            });

            var configuration = testContext.GetRequiredService<IConfiguration>()!;
            webHostBuilder.UseConfiguration(configuration);
            webHostBuilder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                if (_outputHelper != null)
                    logging.Services.AddSerilog(loggerConfiguration =>
                    {
                        loggerConfiguration.MinimumLevel.Override("Microsoft", LogEventLevel.Warning);
                        loggerConfiguration.MinimumLevel.Override("System", LogEventLevel.Warning);
                        loggerConfiguration.WriteTo.TestOutput(_outputHelper, LogEventLevel.Information,
                            "[BEGIN {Timestamp:HH:mm:ss.fffffff} {Level:u3} {SourceContext}]{NewLine}{Message:lj}{NewLine}[END {Timestamp:HH:mm:ss.fffffff} {Level:u3} {SourceContext}]");
                    });
            });
            webHostConfigurator?.Invoke(webHostBuilder);
        });

        _factory = factory;

        return factory;
    }


    /// <summary>
    /// By default, logs are written to test output when debugger is attached in order to not leak sensitive data.
    /// Use test output logger cautiously.
    /// </summary>
    public void LogToTestOutput(ITestOutputHelper outputHelper, bool debugOnly = true)
    {
        if (debugOnly && !Debugger.IsAttached) return;

        _outputHelper = outputHelper;
    }

    public void Dispose() => _factory?.Dispose();
}

public class DrnWebApplicationFactory<TEntryPoint>(TestContext context, bool temporary = false) : WebApplicationFactory<TEntryPoint>
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