using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Utils.Models.Sample;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Tests.Sample.Controller.Sample;

public class WeatherForecastControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task WeatherForecastController_Should_Return_Forecasts(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        var weatherEndpoint = Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var sampleForecasts = await client.GetFromJsonAsync<WeatherForecast[]>(weatherEndpoint);
        var appSettings = context.GetRequiredService<IAppSettings>();

        context.FlurlHttpTest.ForCallsTo($"*{appSettings.NexusAppSettings.NexusAddress}/WeatherForecast").RespondWithJson(sampleForecasts);

        var nexusWeatherEndpoint = Get.Endpoint.Sample.WeatherForecast.GetNexusWeatherForecasts.RoutePattern;
        var nexusForecasts = await client.GetFromJsonAsync<WeatherForecast[]>(nexusWeatherEndpoint);
        nexusForecasts.Should().BeEquivalentTo(sampleForecasts);
    }

    [Theory]
    [DataInline]
    public async Task WeatherForecastController_Should_Return_FlurlHttpExceptionStatusCodes(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        var appSettings = context.GetRequiredService<IAppSettings>();
        var urlPattern = $"*{appSettings.NexusAppSettings.NexusAddress}/WeatherForecast";
        var nexusWeatherEndpoint = Get.Endpoint.Sample.WeatherForecast.GetNexusWeatherForecasts.RoutePattern;

        context.FlurlHttpTest.ForCallsTo(urlPattern).RespondWith("", 428);
        var response = await client.GetAsync(nexusWeatherEndpoint);
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        context.FlurlHttpTest.ClearFilteredSetups().ForCallsTo(urlPattern).RespondWith("", 500);
        response = await client.GetAsync(nexusWeatherEndpoint);
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        context.FlurlHttpTest.ClearFilteredSetups().ForCallsTo(urlPattern).RespondWith("", 503);
        response = await client.GetAsync(nexusWeatherEndpoint);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        context.FlurlHttpTest.ClearFilteredSetups().ForCallsTo(urlPattern).RespondWith("", 504);
        response = await client.GetAsync(nexusWeatherEndpoint);
        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
    }
}