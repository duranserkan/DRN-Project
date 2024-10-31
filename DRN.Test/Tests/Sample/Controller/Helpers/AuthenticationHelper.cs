using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity.Data;
using Sample.Hosted.Controllers;

namespace DRN.Test.Tests.Sample.Controller.Helpers;

public static class AuthenticationHelper
{
    public static async Task<AuthenticatedUserModel> AuthenticateClientAsync(HttpClient client)
    {
        var credentials = CredentialsProvider.TestUserCredentials;
        var user = await AuthenticateClientAsync(client, credentials.Username, credentials.Password);

        return user;
    }

    public static async Task<AuthenticatedUserModel> AuthenticateClientAsync(HttpClient client, string username, string password)
    {
        var registerRequest = new RegisterRequest
        {
            Email = $"{username}@example.com",
            Password = $"{password}1.Ab"
        };

        var token = await GetAccessTokenAsync(client, registerRequest);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        return new AuthenticatedUserModel
        {
            Email = registerRequest.Email,
            Username = username,
            Password = $"{password}1.Ab",
            Token = token
        };
    }

    public static async Task<string> GetAccessTokenAsync(HttpClient client, RegisterRequest registerRequest)
    {
        await RegisterUserAsync(client, registerRequest);

        var responseMessage = await client.PostAsJsonAsync(EndpointFor.User.Identity.Login.RoutePattern, registerRequest);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenResponse = await responseMessage.Content.ReadFromJsonAsync<AccessTokenResponse>();
        tokenResponse?.AccessToken.Should().NotBeNull();

        return tokenResponse?.AccessToken!;
    }

    public static async Task RegisterUserAsync(HttpClient client, RegisterRequest registerRequest)
    {
        var responseMessage = await client.PostAsJsonAsync(EndpointFor.User.Identity.Register.RoutePattern, registerRequest);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}