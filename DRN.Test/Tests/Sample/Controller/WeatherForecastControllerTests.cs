using System.Net.Http.Json;
using DRN.Framework.Utils.Models;
using Sample.Hosted;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Sample.Controller;

public class WeatherForecastControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task WeatherForecastController_Should_Return_Forecasts(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        var sampleForecasts = await client.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast");

        context.FlurlHttpTest.ForCallsTo("*nexus/WeatherForecast").RespondWithJson(sampleForecasts);
        var nexusForecasts = await client.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast/nexus");

        nexusForecasts.Should().BeEquivalentTo(sampleForecasts);
    }
}