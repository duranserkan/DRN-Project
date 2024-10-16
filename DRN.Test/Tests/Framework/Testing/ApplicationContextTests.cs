using System.Net.Http.Json;
using DRN.Framework.Utils.Models;
using DRN.Framework.Utils.Models.Sample;
using Sample.Hosted;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Framework.Testing;

public class ApplicationContextTests
{
    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Provide_Configuration_To_Program(TestContext context)
    {
        var webApplication = context.ApplicationContext.CreateApplication<Program>();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();

        var client = webApplication.CreateClient();
        var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast");
        forecasts.Should().NotBeNull();

        var appSettingsFromWebApplication = webApplication.Services.GetRequiredService<IAppSettings>();
        var connectionString = appSettingsFromWebApplication.GetRequiredConnectionString(nameof(QAContext));
        connectionString.Should().NotBeNull();

        var appSettingsFromTestContext = context.GetRequiredService<IAppSettings>();
        appSettingsFromWebApplication.Should().BeSameAs(appSettingsFromTestContext); //resolved from same service provider

        //comes from settings.json in test project's global data directory
        var duckTest = "If it looks like a duck, swims like a duck, and quacks like a duck, then it probably is a duck";
        appSettingsFromTestContext.GetValue("DuckTest", "").Should().Be(duckTest);

        //comes from appsettings.json in web application's directory
        var saganStandard = "Extraordinary claims require extraordinary evidence";
        appSettingsFromTestContext.GetValue("SaganStandard", "").Should().Be(saganStandard);

        //appsettings.json value is overriden by settings.json
        var philosophicalRazor = "Never attribute to malice that which can be adequately explained by incompetence or stupidity";
        appSettingsFromTestContext.GetValue("PhilosophicalRazor", "").Should().Be(philosophicalRazor);
    }
}