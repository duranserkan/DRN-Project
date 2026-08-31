using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity.Data;

namespace DRN.Test.Integration.Tests.Sample.Controller.Helpers;

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
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestUser.Token);
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
        if (string.IsNullOrWhiteSpace(endpoints.TwoFactorAuthUrl))
            throw ExceptionFor.Validation($"{nameof(endpoints.TwoFactorAuthUrl)} can not be null or whitespace");

        await RegisterUserAsync(client, registerRequest, endpoints);

        var setupLoginResponse = await client.PostAsJsonAsync(endpoints.LoginUrl, new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password
        });
        setupLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var setupTokenResponse = await setupLoginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        setupTokenResponse.Should().NotBeNull();
        setupTokenResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();
        setupTokenResponse.RefreshToken.Should().BeEmpty();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", setupTokenResponse.AccessToken);

        var setupResponse = await client.PostAsJsonAsync(endpoints.TwoFactorAuthUrl, new TwoFactorRequest());
        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var twoFactorResponse = await setupResponse.Content.ReadFromJsonAsync<TwoFactorResponse>();
        twoFactorResponse.Should().NotBeNull();
        twoFactorResponse!.SharedKey.Should().NotBeNullOrWhiteSpace();

        var twoFactorCode = TotpUtils.GenerateTotpCode(twoFactorResponse.SharedKey);
        var enableResponse = await client.PostAsJsonAsync(endpoints.TwoFactorAuthUrl, new TwoFactorRequest
        {
            Enable = true,
            TwoFactorCode = twoFactorCode
        });
        enableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await client.PostAsJsonAsync(endpoints.LoginUrl, new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password,
            TwoFactorCode = TotpUtils.GenerateTotpCode(twoFactorResponse.SharedKey)
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        tokenResponse.Should().NotBeNull();
        tokenResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();

        return tokenResponse.AccessToken;
    }

    public static async Task RegisterUserAsync(HttpClient client, RegisterRequest registerRequest, AuthenticationEndpoints endpoints)
    {
        var responseMessage = await client.PostAsJsonAsync(endpoints.RegisterUrl, registerRequest);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public record AuthenticationEndpoints(string LoginUrl, string RegisterUrl, string? TwoFactorAuthUrl = null);
