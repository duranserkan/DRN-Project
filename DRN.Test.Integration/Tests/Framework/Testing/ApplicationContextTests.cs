using System.Net.Http.Json;
using DRN.Framework.Utils.Models.Sample;
using DRN.Test.Utils.Hosting;
using Microsoft.AspNetCore.Hosting;
using Sample.Hosted;
using Sample.Hosted.Helpers;
using Sample.Infra.QA;

namespace DRN.Test.Integration.Tests.Framework.Testing;

public class ApplicationContextTests
{
    [Theory]
    [DataInline]
    public void ApplicationContext_Should_Create_Sequential_Clients(DrnTestContext context)
    {
        var firstApplication = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>();
        using var firstClient = firstApplication.CreateClient();

        var secondApplication = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>();
        using var secondClient = secondApplication.CreateClient();

        secondClient.Should().NotBeSameAs(firstClient);
    }

    [Theory]
    [DataInline]
    public void DrnTestContext_Should_Dispose_Application_Before_Owned_ServiceProvider(DrnTestContext context)
    {
        var disposalOrder = new List<string>();
        context.ServiceCollection.AddSingleton(_ => new ContextOwnedDisposalTracker(disposalOrder));
        _ = context.GetRequiredService<ContextOwnedDisposalTracker>();

        var application = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(_ => new ApplicationOwnedDisposalTracker(disposalOrder))));
        _ = application.Services.GetRequiredService<ApplicationOwnedDisposalTracker>();

        context.Dispose();

        disposalOrder.Should().Equal("application", "context");
    }

    [Theory]
    [DataInline]
    public void ApplicationContext_Hosts_Should_Not_Emit_Lifecycle_Logs(DrnTestContext context)
    {
        TemporaryLifecycleProgram.Reset();

        var application = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>();
        _ = application.Server;

        TemporaryLifecycleProgram.CapturedLifecycleLogCount.Should().Be(0);
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Provide_Configuration_To_Program(DrnTestContext context)
    {
        var webApplication = context.ApplicationContext.CreateApplication<SampleProgram>();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();

        var client = webApplication.CreateClient();
        var endpoint = Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>(endpoint);
        forecasts.Should().NotBeNull();

        var appSettingsFromWebApplication = webApplication.Services.GetRequiredService<IAppSettings>();
        var connectionString = appSettingsFromWebApplication.GetRequiredConnectionString(nameof(QAContext));
        connectionString.Should().NotBeNull();

        var appSettingsFromDrnTestContext = context.GetRequiredService<IAppSettings>();
        appSettingsFromWebApplication.Should().BeSameAs(appSettingsFromDrnTestContext); //resolved from same service provider

        //comes from settings.json in test project's global data directory
        var duckTest = "If it looks like a duck, swims like a duck, and quacks like a duck, then it probably is a duck";
        appSettingsFromDrnTestContext.GetValue("DuckTest", "").Should().Be(duckTest);

        //comes from appsettings.json in web application's directory
        var saganStandard = "Extraordinary claims require extraordinary evidence";
        appSettingsFromDrnTestContext.GetValue("SaganStandard", "").Should().Be(saganStandard);

        //appsettings.json value is overriden by settings.json
        var philosophicalRazor = "Never attribute to malice that which can be adequately explained by incompetence or stupidity";
        appSettingsFromDrnTestContext.GetValue("PhilosophicalRazor", "").Should().Be(philosophicalRazor);
    }

    private sealed class ContextOwnedDisposalTracker(List<string> disposalOrder) : IDisposable
    {
        public void Dispose() => disposalOrder.Add("context");
    }

    private sealed class ApplicationOwnedDisposalTracker(List<string> disposalOrder) : IDisposable
    {
        public void Dispose() => disposalOrder.Add("application");
    }
}
