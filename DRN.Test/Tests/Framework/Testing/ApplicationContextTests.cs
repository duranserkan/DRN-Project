using System.Net.Http.Json;
using DRN.Framework.Utils.Models;
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
    }
}