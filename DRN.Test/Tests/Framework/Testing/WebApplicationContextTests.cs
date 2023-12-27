using System.Net.Http.Json;
using Sample.Hosted;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Framework.Testing;

public class WebApplicationContextTests
{
    [TheoryDebuggerOnly]
    [DataInline]
    public async Task WebApplicationContext_Should_Provide_Configuration_To_Program(TestContext context)
    {
        var webApplication = context.WebApplicationContext.CreateWebApplication<Program>();
        await context.ContainerContext.StartPostgresAndApplyMigrationsAsync();

        var client = webApplication.CreateClient();
        var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast");
        forecasts.Should().NotBeNull();

        var appSettings = webApplication.Services.GetRequiredService<IAppSettings>();
        var connectionString = appSettings.GetRequiredConnectionString(nameof(QAContext));
        connectionString.Should().NotBeNull();
    }
}