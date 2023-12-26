using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using IConfiguration = Castle.Core.Configuration.IConfiguration;

namespace DRN.Framework.Testing.Contexts;

public sealed class WebApplicationContext(TestContext testContext) : IDisposable
{
    private readonly List<IDisposable> _factories = [];

    public WebApplicationFactory<TEntryPoint> CreateWebApplication<TEntryPoint>(Action<IWebHostBuilder>? configuration = null)
        where TEntryPoint : class
    {
        var factory = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(webHostBuilder =>
        {
            webHostBuilder.ConfigureServices(serviceCollection =>
            {
                testContext.ServiceCollection.Add(serviceCollection);
                testContext.MethodContext.ReplaceSubstitutedInterfaces(serviceCollection);
                testContext.MethodContext.ReplaceSubstitutedInterfaces(testContext.ServiceCollection);
            });

            webHostBuilder.UseConfiguration(testContext.BuildConfigurationRoot());
            webHostBuilder.ConfigureLogging(x => x.ClearProviders());
            configuration?.Invoke(webHostBuilder);
        });


        return factory;
    }

    public void Dispose()
    {
        foreach (var factory in _factories) factory.Dispose();
    }
}