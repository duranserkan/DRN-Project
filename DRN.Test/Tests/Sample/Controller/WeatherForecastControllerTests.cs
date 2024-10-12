using System.Net;
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
        var appSettings = context.GetRequiredService<IAppSettings>();

        context.FlurlHttpTest.ForCallsTo($"*{appSettings.Features.NexusAddress}/WeatherForecast").RespondWithJson(sampleForecasts);

        var nexusForecasts = await client.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast/nexus");
        nexusForecasts.Should().BeEquivalentTo(sampleForecasts);
    }

    [Theory]
    [DataInline]
    public async Task WeatherForecastController_Should_Return_FlurlHttpExceptionStatusCodes(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        var appSettings = context.GetRequiredService<IAppSettings>();
        var urlPattern = $"*{appSettings.Features.NexusAddress}/WeatherForecast";

        context.FlurlHttpTest.ForCallsTo(urlPattern).RespondWith("", 428);
        var response = await client.GetAsync("WeatherForecast/nexus");
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        context.FlurlHttpTest.ClearFilteredSetups().ForCallsTo(urlPattern).RespondWith("", 500);
        response = await client.GetAsync("WeatherForecast/nexus");
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        context.FlurlHttpTest.ClearFilteredSetups().ForCallsTo(urlPattern).RespondWith("", 503);
        response = await client.GetAsync("WeatherForecast/nexus");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        context.FlurlHttpTest.ClearFilteredSetups().ForCallsTo(urlPattern).RespondWith("", 504);
        response = await client.GetAsync("WeatherForecast/nexus");
        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
    }
}