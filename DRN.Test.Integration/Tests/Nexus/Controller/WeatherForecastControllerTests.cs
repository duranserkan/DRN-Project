using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Utils.Models.Sample;
using DRN.Nexus.Hosted;
using DRN.Nexus.Hosted.Helpers;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;

namespace DRN.Test.Integration.Tests.Nexus.Controller;

public class WeatherForecastControllerTests
{
    [Theory]
    [DataInline]
    public async Task WeatherForecastController_Should_Return_Forecasts(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        var weatherEndpoint = Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var sampleForecasts = await client.GetFromJsonAsync<WeatherForecast[]>(weatherEndpoint);

        sampleForecasts!.Length.Should().BePositive();
    }

    [Theory]
    [DataInline]
    public async Task PrivateAction_Should_Not_Allow_Unauthorized(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        var status = await client.GetAsync(Get.Endpoint.Sample.WeatherForecast.Private.RoutePattern);

        status.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [DataInline]
    public async Task PrivateAction_Should_Return_Status(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        var user = await AuthenticationHelper<NexusProgram>.AuthenticateClientAsync(client);

        var authorized = await client.GetStringAsync(Get.Endpoint.Sample.WeatherForecast.Private.RoutePattern);
        authorized.Should().Be("authorized");
    }
}
