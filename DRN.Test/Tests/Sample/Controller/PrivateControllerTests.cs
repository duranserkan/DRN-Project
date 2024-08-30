using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Utils.Auth;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity.Data;
using Sample.Hosted;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Sample.Controller;

public class PrivateControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task Authorized_Action_Should_Return_AuthenticatedUser(TestContext context, string username, string password)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        await AuthenticateClient(client, username, password);

        var user = await client.GetFromJsonAsync<ScopedUserSummary>("private");
        user.Should().NotBeNull();
        user?.Authenticated.Should().BeTrue();
    }

    [Theory]
    [DataInline]
    public async Task Anonymous_Action_Should_Return_AnonymousUser(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);

        var user = await client.GetFromJsonAsync<ScopedUserSummary>("private/anonymous");
        user.Should().NotBeNull();
        user?.Authenticated.Should().BeFalse();
    }

    [Theory]
    [DataInline]
    public async Task Validate_Scope_Action_Should_Request_ScopeContext(TestContext context, string username, string password)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        await AuthenticateClient(client, username, password);

        var response = await client.GetAsync("private/scope-context");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Validate_Scope_Action_Should_Validate_Scope(TestContext context, string username, string password)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        await AuthenticateClient(client, username, password);

        var response = await client.GetAsync("private/validate-scope");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task AuthenticateClient(HttpClient client, string username, string password)
    {
        var registerRequest = new RegisterRequest
        {
            Email = $"{username}@example.com",
            Password = $"{password}1.Ab"
        };

        var token = await GetAccessToken(client, registerRequest);

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    private static async Task<string> GetAccessToken(HttpClient client, RegisterRequest registerRequest)
    {
        await RegisterUser(client, registerRequest);

        var responseMessage = await client.PostAsJsonAsync("identity/login", registerRequest);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenResponse = await responseMessage.Content.ReadFromJsonAsync<AccessTokenResponse>();
        tokenResponse?.AccessToken.Should().NotBeNull();

        return tokenResponse?.AccessToken!;
    }

    private static async Task RegisterUser(HttpClient client, RegisterRequest registerRequest)
    {
        var responseMessage = await client.PostAsJsonAsync("identity/register", registerRequest);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}