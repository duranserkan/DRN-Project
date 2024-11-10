using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Hosting.DrnProgram;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity.Data;

namespace DRN.Test.Tests.Sample.Controller.Helpers;

public abstract class AuthenticationHelper<TProgram> : AuthenticationHelper where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    private static AuthenticatedUserModel? TestUser;
    public static AuthenticationEndpoints AuthEndpoints { get; set; } = null!;

    public static async Task<AuthenticatedUserModel> AuthenticateClientAsync(HttpClient client)
    {
        if (AuthEndpoints == null)
            throw ExceptionFor.Validation($"{nameof(AuthEndpoints)} can not be null");

        if (TestUser != null)
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestUser.Token}");
            return TestUser;
        }

        var credentials = CredentialsProvider.Credentials;
        TestUser = await AuthenticateClientAsync(client, credentials, AuthEndpoints);

        return TestUser;
    }
}

public abstract class AuthenticationHelper
{
    public static async Task<AuthenticatedUserModel> AuthenticateClientAsync(HttpClient client, AuthenticationEndpoints endpoints)
    {
        var credentials = CredentialsProvider.Credentials;
        var user = await AuthenticateClientAsync(client, credentials, endpoints);

        return user;
    }

    public static async Task<AuthenticatedUserModel> AuthenticateClientAsync(HttpClient client,
        TestUserCredentials credentials,
        AuthenticationEndpoints endpoints)
    {
        var registerRequest = new RegisterRequest
        {
            Email = $"{credentials.Username}@example.com",
            Password = credentials.Password
        };

        var token = await GetAccessTokenAsync(client, registerRequest, endpoints);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        return new AuthenticatedUserModel
        {
            Email = registerRequest.Email,
            Username = credentials.Username,
            Password = credentials.Password,
            Token = token
        };
    }

    public static async Task<string> GetAccessTokenAsync(HttpClient client, RegisterRequest registerRequest, AuthenticationEndpoints endpoints)
    {
        await RegisterUserAsync(client, registerRequest, endpoints);

        var responseMessage = await client.PostAsJsonAsync(endpoints.LoginUrl, registerRequest);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenResponse = await responseMessage.Content.ReadFromJsonAsync<AccessTokenResponse>();
        tokenResponse?.AccessToken.Should().NotBeNull();

        return tokenResponse?.AccessToken!;
    }

    public static async Task RegisterUserAsync(HttpClient client, RegisterRequest registerRequest, AuthenticationEndpoints endpoints)
    {
        var responseMessage = await client.PostAsJsonAsync(endpoints.RegisterUrl, registerRequest);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public record AuthenticationEndpoints(string LoginUrl, string RegisterUrl);