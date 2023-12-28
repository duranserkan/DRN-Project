using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Testing.Contexts;

public sealed class WebApplicationContext(TestContext testContext) : IDisposable
{
    private IDisposable? _factory = null;


    public WebApplicationFactory<TEntryPoint> CreateWebApplication<TEntryPoint>(Action<IWebHostBuilder>? webHostConfigurator = null)
        where TEntryPoint : class
    {
        Dispose();

        //Add program services to testContext
        var tempApplicationFactory = new DrnWebApplicationFactory<TEntryPoint>(testContext).WithWebHostBuilder(webHostBuilder =>
        {
            webHostBuilder.ConfigureServices(serviceCollection => testContext.ServiceCollection.Add(serviceCollection));
            webHostConfigurator?.Invoke(webHostBuilder);
        });
        _ = tempApplicationFactory.Server; //To trigger webHostBuilder action
        tempApplicationFactory.Dispose();

        //register action to pass test context configuration to web application.
        //This will be triggered when TestServer or HttpClient requested until then further configurations can be added to test context configuration
        var factory = new DrnWebApplicationFactory<TEntryPoint>(testContext).WithWebHostBuilder(webHostBuilder =>
        {
            webHostBuilder.ConfigureServices(serviceCollection =>
            {
                testContext.MethodContext.ReplaceSubstitutedInterfaces(serviceCollection);
                testContext.MethodContext.ReplaceSubstitutedInterfaces(testContext.ServiceCollection);
            });

            var configuration = testContext.GetRequiredService<IConfiguration>()!;
            webHostBuilder.UseConfiguration(configuration);
            webHostBuilder.ConfigureLogging(x => x.ClearProviders());
            webHostConfigurator?.Invoke(webHostBuilder);
        });

        _factory = factory;

        return factory!;
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}

public class DrnWebApplicationFactory<TEntryPoint>(TestContext context) : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        context.OverrideServiceProvider(host.Services);

        return host;
    }
}