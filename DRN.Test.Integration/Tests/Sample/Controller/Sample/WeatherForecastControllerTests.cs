using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Hosting.Nexus;
using DRN.Framework.Utils.Models.Sample;
using DRN.Nexus.Hosted;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Controller.Sample;

public class WeatherForecastControllerTests
{
    [Theory]
    [DataInline]
    public async Task WeatherForecastController_Should_Return_Forecasts_And_Map_Upstream_Failures(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        var weatherEndpoint = Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var sampleForecasts = await client.GetFromJsonAsync<WeatherForecast[]>(weatherEndpoint);
        var appSettings = context.GetRequiredService<IAppSettings>();

        var urlPattern = $"*{appSettings.NexusAppSettings.NexusAddress}/{NexusEndpoints.WeatherForecast}";
        context.FlurlHttpTest.ForCallsTo(urlPattern).RespondWithJson(sampleForecasts);

        var nexusWeatherEndpoint = Get.Endpoint.Sample.WeatherForecast.GetNexusWeatherForecasts.RoutePattern;
        var nexusForecasts = await client.GetFromJsonAsync<WeatherForecast[]>(nexusWeatherEndpoint);
        nexusForecasts.Should().BeEquivalentTo(sampleForecasts);

        foreach (var (upstreamStatus, expectedStatus) in new[]
                 {
                     (428, HttpStatusCode.PreconditionRequired), (500, HttpStatusCode.BadGateway),
                     (503, HttpStatusCode.ServiceUnavailable), (504, HttpStatusCode.GatewayTimeout)
                 })
        {
            context.FlurlHttpTest.ClearFilteredSetups().ForCallsTo(urlPattern).RespondWith("", upstreamStatus);
            using var response = await client.GetAsync(nexusWeatherEndpoint);
            response.StatusCode.Should().Be(expectedStatus, "upstream HTTP {0} must use the expected mapping", upstreamStatus);
        }
    }

    [Theory]
    [DataInline]
    public async Task WeatherForecastController_Should_Return_Nexus_Forecasts_From_Hosted_Nexus(DrnTestContext context)
    {
        _ = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        var sampleClient = await context.ApplicationContext.CreateClientAsync<SampleProgram>();

        var sampleWeatherEndpoint = Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var sampleForecasts = await sampleClient.GetFromJsonAsync<WeatherForecast[]>(sampleWeatherEndpoint);
        sampleForecasts.Should().NotBeNull();
        sampleForecasts.Length.Should().BePositive();

        var nexusWeatherEndpoint = Get.Endpoint.Sample.WeatherForecast.GetNexusWeatherForecasts.RoutePattern;
        var nexusForecasts = await sampleClient.GetFromJsonAsync<WeatherForecast[]>(nexusWeatherEndpoint);

        nexusForecasts.Should().NotBeNull();
        nexusForecasts.Length.Should().BePositive();
    }
}
