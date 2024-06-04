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

        context.FlurlHttpTest.ForCallsTo("*nexus/WeatherForecast").RespondWithJson(sampleForecasts);

        var nexusForecasts = await client.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast/nexus");
        nexusForecasts.Should().BeEquivalentTo(sampleForecasts);
    }

    [Theory]
    [DataInline]
    public async Task WeatherForecastController_Should_Return_DrnException_Status_Codes(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        var response = await client.GetAsync("WeatherForecast/ValidationException");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        response = await client.GetAsync("WeatherForecast/UnauthorizedException");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        response = await client.GetAsync("WeatherForecast/ForbiddenException");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        response = await client.GetAsync("WeatherForecast/NotFoundException");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        response = await client.GetAsync("WeatherForecast/ConflictException");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        response = await client.GetAsync("WeatherForecast/ExpiredException");
        response.StatusCode.Should().Be(HttpStatusCode.Gone);

        response = await client.GetAsync("WeatherForecast/ConfigurationException");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        response = await client.GetAsync("WeatherForecast/UnprocessableEntityException");
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        Func<Task> malicious = async () => await client.GetAsync("WeatherForecast/MaliciousRequestException");
        await malicious.Should().ThrowAsync<OperationCanceledException>("The application aborted the request.");
    }

    [Theory]
    [DataInline]
    public async Task WeatherForecastController_Should_Return_FlurlHttpExceptionStatusCodes(TestContext context)
    {
        var urlPattern = "*nexus/WeatherForecast";
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);

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