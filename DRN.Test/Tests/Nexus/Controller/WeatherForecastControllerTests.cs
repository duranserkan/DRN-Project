using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Utils.Models.Sample;
using DRN.Nexus.Hosted;
using DRN.Nexus.Hosted.Controllers;
using DRN.Nexus.Hosted.Helpers;
using DRN.Test.Tests.Sample.Controller.Helpers;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Nexus.Controller;

public class WeatherForecastControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task WeatherForecastController_Should_Return_Forecasts(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<NexusProgram>(outputHelper);
        var weatherEndpoint = Get.Endpoint.WeatherForecast.Get.RoutePattern;
        var sampleForecasts = await client.GetFromJsonAsync<WeatherForecast[]>(weatherEndpoint);

        sampleForecasts!.Length.Should().BePositive();
    }

    [Theory]
    [DataInline]
    public async Task PrivateAction_Should_Not_Allow_Unauthorized(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<NexusProgram>(outputHelper);
        var status = await client.GetAsync(Get.Endpoint.WeatherForecast.Private.RoutePattern);

        status.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [DataInline]
    public async Task PrivateAction_Should_Return_Status(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<NexusProgram>(outputHelper);
        var user = await AuthenticationHelper<NexusProgram>.AuthenticateClientAsync(client);

        var authorized = await client.GetStringAsync(Get.Endpoint.WeatherForecast.Private.RoutePattern);
        authorized.Should().Be("authorized");
    }
}