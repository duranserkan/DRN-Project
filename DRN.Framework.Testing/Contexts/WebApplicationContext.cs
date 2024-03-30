using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Testing.Contexts;

public sealed class WebApplicationContext(TestContext testContext) : IDisposable
{
    private IDisposable? _factory;

    public WebApplicationFactory<TEntryPoint> CreateWebApplication<TEntryPoint>(Action<IWebHostBuilder>? webHostConfigurator = null)
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