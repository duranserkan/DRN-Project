using System.Net;
using System.Net.Http.Json;
using DRN.Nexus.Hosted;
using DRN.Nexus.Hosted.Controllers;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity.Data;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Nexus.Controller;

public class WeatherForecastControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task PrivateAction_Should_Return_Status(TestContext context, string username, string password)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        var registerRequest = new RegisterRequest
        {
            Email = $"{username}@example.com",
            Password = $"{password}1.Ab"
        };

        var responseMessage = await client.PostAsJsonAsync("account/register", registerRequest);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);

        responseMessage = await client.PostAsJsonAsync("account/login", registerRequest);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenResponse = await responseMessage.Content.ReadFromJsonAsync<AccessTokenResponse>();
        tokenResponse?.AccessToken.Should().NotBeNull();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResponse!.AccessToken}");

        var authorized = await client.GetStringAsync(EndpointFor.WeatherForecast.Private.RoutePattern);
        authorized.Should().Be("authorized");
    }

    [Theory]
    [DataInline]
    public async Task PrivateAction_Should_Not_Allow_Unauthorized(TestContext context)
    {
        var application = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<Program>(outputHelper);

        var client = application.CreateClient();
        var status = await client.GetAsync(EndpointFor.WeatherForecast.Private.RoutePattern);
        status.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}