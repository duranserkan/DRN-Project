using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Utils.Configurations;
using DRN.Nexus.Hosted;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity.Data;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Nexus.Controller;

public class StatusControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task StatusController_Should_Return_Status(TestContext context, string username, string password)
    {
        context.ApplicationContext.LogToTestOutput(outputHelper);
        var application = context.ApplicationContext.CreateApplication<Program>();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();

        var client = application.CreateClient();
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
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResponse.AccessToken}");

        var status = await client.GetFromJsonAsync<ConfigurationDebugViewSummary>("Status");
        var programName = typeof(Program).GetAssemblyName();
        status?.ApplicationName.Should().Be(programName);
    }

    [Theory]
    [DataInline]
    public async Task StatusController_Should_Not_Allow_Unauthorized(TestContext context, string password)
    {
        context.ApplicationContext.LogToTestOutput(outputHelper);
        var application = context.ApplicationContext.CreateApplication<Program>();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();

        var client = application.CreateClient();
        var status = await client.GetAsync("Status");
        status.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}