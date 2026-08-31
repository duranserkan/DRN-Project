using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;
using DRN.Test.Utils.Hosting.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sample.Domain.Users;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Integration.Tests.Framework.Hosting.Auth;

public class BearerMfaEnforcementTests
{
    [Theory]
    [DataInline]
    public async Task Enforced_Mfa_Login_Should_Not_Disclose_User_Or_Enrollment_State(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        var identity = Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(
            identity.LoginController.Login.RoutePattern!,
            identity.RegisterController.Register.RoutePattern!,
            identity.ManagementController.TwoFactorAuth.RoutePattern!);
        var credentials = CredentialsProvider.GenerateCredentials();
        var registeredEmail = $"{credentials.Username}@example.com";
        await AuthenticationHelper.RegisterUserAsync(client, new RegisterRequest
        {
            Email = registeredEmail,
            Password = credentials.Password
        }, endpoints);

        var enrolledCredentials = CredentialsProvider.GenerateCredentials();
        var enrolledEmail = $"{enrolledCredentials.Username}@example.com";
        _ = await AuthenticationHelper.GetAccessTokenAsync(client, new RegisterRequest
        {
            Email = enrolledEmail,
            Password = enrolledCredentials.Password
        }, endpoints);

        var missingUserFailure = await GetLoginFailureAsync(client, endpoints.LoginUrl, $"missing-{registeredEmail}", credentials.Password);
        var registeredUserFailure = await GetLoginFailureAsync(client, endpoints.LoginUrl, registeredEmail, $"{credentials.Password}-invalid");
        var incompleteMfaFailure = await GetLoginFailureAsync(client, endpoints.LoginUrl, enrolledEmail, enrolledCredentials.Password);

        missingUserFailure.Should().Be("Invalid email or password.");
        registeredUserFailure.Should().Be(missingUserFailure);
        incompleteMfaFailure.Should().Be(missingUserFailure);
    }

    [Theory]
    [DataInline]
    public async Task Exempt_Authentication_Scheme_Should_Not_Authorize_Mfa_Setup_Credential(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<MfaExemptionPipelineTestProgram>();

        await AssertPipelineStatusAsync(client, credential: null, HttpStatusCode.Unauthorized);
        await AssertPipelineStatusAsync(client, MfaPipelineTestValues.InvalidCredential, HttpStatusCode.Unauthorized);
        await AssertPipelineStatusAsync(client, MfaPipelineTestValues.PasswordCredential, HttpStatusCode.OK);
        await AssertPipelineStatusAsync(client, MfaPipelineTestValues.SetupCredential, HttpStatusCode.Forbidden);
        await AssertPipelineStatusAsync(client, MfaPipelineTestValues.SetupAndCompletedCredential, HttpStatusCode.Forbidden);
        await AssertPipelineStatusAsync(client, MfaPipelineTestValues.CompletedCredential, HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Role_Only_Policy_Should_Not_Bypass_Global_Mfa_Enforcement(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<MfaExemptionPipelineTestProgram>();
        var path = MfaPipelineTestValues.RoleProtectedPath;

        await AssertPipelineStatusAsync(client, credential: null, HttpStatusCode.Unauthorized, path);
        await AssertPipelineStatusAsync(client, MfaPipelineTestValues.PasswordCredential, HttpStatusCode.OK, path);
        await AssertPipelineStatusAsync(client, MfaPipelineTestValues.SetupCredential, HttpStatusCode.Forbidden, path);
        await AssertPipelineStatusAsync(client, MfaPipelineTestValues.SetupAndCompletedCredential, HttpStatusCode.Forbidden, path);
        await AssertPipelineStatusAsync(client, MfaPipelineTestValues.CompletedCredential, HttpStatusCode.OK, path);
    }

    [Theory]
    [DataInline]
    public async Task Named_Policy_Should_Authenticate_With_Its_Configured_Scheme(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<MfaExemptionPipelineTestProgram>();
        using var request = new HttpRequestMessage(HttpMethod.Get, MfaPipelineTestValues.NamedSchemeProtectedPath);
        request.Headers.Add(MfaPipelineTestValues.CredentialHeader, MfaPipelineTestValues.CompletedCredential);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(MfaPipelineTestValues.NamedAuthenticationScheme);
    }

    [Theory]
    [DataInline]
    public async Task Named_Policy_Should_Reject_When_Configured_Scheme_Authentication_Fails(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<MfaExemptionPipelineTestProgram>();
        using var request = new HttpRequestMessage(HttpMethod.Get, MfaPipelineTestValues.NamedSchemeProtectedPath);
        request.Headers.Add(MfaPipelineTestValues.CredentialHeader, MfaPipelineTestValues.InvalidCredential);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [DataInline]
    public async Task Non_Default_Exempt_Scheme_Must_Not_Authenticate_Unrelated_Endpoints(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<NonDefaultExemptSchemeTestProgram>();

        // 1. Send ApiKey credential to cookie-only role-protected endpoint (which never selected ApiKey).
        // It must NOT authenticate the endpoint and must return 401 Unauthorized.
        using var cookieEndpointRequest = new HttpRequestMessage(HttpMethod.Get, NonDefaultExemptValues.CookieRoleProtectedPath);
        cookieEndpointRequest.Headers.Add(NonDefaultExemptValues.ApiKeyHeader, "api-key-valid");
        using var cookieEndpointResponse = await client.SendAsync(cookieEndpointRequest);
        cookieEndpointResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Send Cookie credential to the cookie-only role-protected endpoint.
        // It must succeed with 200 OK.
        using var cookieValidRequest = new HttpRequestMessage(HttpMethod.Get, NonDefaultExemptValues.CookieRoleProtectedPath);
        cookieValidRequest.Headers.Add(NonDefaultExemptValues.CookieHeader, "cookie-valid");
        using var cookieValidResponse = await client.SendAsync(cookieValidRequest);
        cookieValidResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Send ApiKey credential to the endpoint explicitly opting into ApiKey.
        // It must succeed with 200 OK and be exempt from MFA.
        using var apiKeyEndpointRequest = new HttpRequestMessage(HttpMethod.Get, NonDefaultExemptValues.ApiKeyProtectedPath);
        apiKeyEndpointRequest.Headers.Add(NonDefaultExemptValues.ApiKeyHeader, "api-key-valid");
        using var apiKeyEndpointResponse = await client.SendAsync(apiKeyEndpointRequest);
        apiKeyEndpointResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Send BOTH Cookie credential (with completed MFA) AND ApiKey credential to the ApiKey endpoint.
        // Ambient completed MFA on the cookie must NOT suppress ApiKey exemption discovery, and must succeed with 200 OK.
        using var combinedRequest = new HttpRequestMessage(HttpMethod.Get, NonDefaultExemptValues.ApiKeyProtectedPath);
        combinedRequest.Headers.Add(NonDefaultExemptValues.CookieHeader, "cookie-valid");
        combinedRequest.Headers.Add(NonDefaultExemptValues.ApiKeyHeader, "api-key-valid");
        using var combinedResponse = await client.SendAsync(combinedRequest);
        combinedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Bearer_Mfa_Flow_Should_Reject_Setup_Token_And_Preserve_Mfa_After_Refresh(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        var identity = Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(
            identity.LoginController.Login.RoutePattern!,
            identity.RegisterController.Register.RoutePattern!,
            identity.ManagementController.TwoFactorAuth.RoutePattern!);
        var twoFactorUrl = endpoints.TwoFactorAuthUrl!;
        var refreshUrl = identity.LoginController.Refresh.RoutePattern!;
        var protectedUrl = identity.ManagementController.GetInfo.RoutePattern!;
        var credentials = CredentialsProvider.GenerateCredentials();
        var registerRequest = new RegisterRequest
        {
            Email = $"{credentials.Username}@example.com",
            Password = credentials.Password
        };
        await AuthenticationHelper.RegisterUserAsync(client, registerRequest, endpoints);

        using var setupLoginResponse = await client.PostAsJsonAsync(endpoints.LoginUrl, new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password
        });
        setupLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var setupToken = await setupLoginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        setupToken.Should().NotBeNull();
        setupToken.AccessToken.Should().NotBeNullOrWhiteSpace();
        setupToken.RefreshToken.Should().BeEmpty();
        setupToken.ExpiresIn.Should().Be(300);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", setupToken.AccessToken);
        using var setupProtectedResponse = await client.GetAsync(protectedUrl);
        setupProtectedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var sharedKeyResponse = await client.PostAsJsonAsync(twoFactorUrl, new TwoFactorRequest());
        sharedKeyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var twoFactor = await sharedKeyResponse.Content.ReadFromJsonAsync<TwoFactorResponse>();
        twoFactor.Should().NotBeNull();
        twoFactor.SharedKey.Should().NotBeNullOrWhiteSpace();

        using var enableResponse = await client.PostAsJsonAsync(twoFactorUrl, new TwoFactorRequest
        {
            Enable = true,
            TwoFactorCode = TotpUtils.GenerateTotpCode(twoFactor.SharedKey)
        });
        enableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = null;
        using var mfaLoginResponse = await client.PostAsJsonAsync(endpoints.LoginUrl, new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password,
            TwoFactorCode = TotpUtils.GenerateTotpCode(twoFactor.SharedKey)
        });
        mfaLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var mfaToken = await mfaLoginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        mfaToken.Should().NotBeNull();
        mfaToken.AccessToken.Should().NotBeNullOrWhiteSpace();
        mfaToken.RefreshToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mfaToken.AccessToken);
        using var mfaProtectedResponse = await client.GetAsync(protectedUrl);
        mfaProtectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = null;
        using var refreshResponse = await client.PostAsJsonAsync(refreshUrl, new RefreshRequest
        {
            RefreshToken = mfaToken.RefreshToken
        });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedToken = await refreshResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        refreshedToken.Should().NotBeNull();
        refreshedToken.AccessToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken.AccessToken);
        using var refreshedProtectedResponse = await client.GetAsync(protectedUrl);
        refreshedProtectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Optional_Mfa_Should_Allow_Authenticated_User_To_Start_Enrollment(DrnTestContext context)
    {
        using var client = await CreateOptionalMfaClientAsync(context);
        var identity = Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(
            identity.LoginController.Login.RoutePattern!,
            identity.RegisterController.Register.RoutePattern!,
            identity.ManagementController.TwoFactorAuth.RoutePattern!);
        var twoFactorUrl = endpoints.TwoFactorAuthUrl!;
        var credentials = CredentialsProvider.GenerateCredentials();
        var registerRequest = new RegisterRequest
        {
            Email = $"{credentials.Username}@example.com",
            Password = credentials.Password
        };
        await AuthenticationHelper.RegisterUserAsync(client, registerRequest, endpoints);

        using var loginResponse = await client.PostAsJsonAsync(endpoints.LoginUrl, new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password,
            TwoFactorCode = null,
            TwoFactorRecoveryCode = null
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        token.Should().NotBeNull();
        token.AccessToken.Should().NotBeNullOrWhiteSpace();
        token.RefreshToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var setupResponse = await client.PostAsJsonAsync(twoFactorUrl, new TwoFactorRequest());
        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var twoFactor = await setupResponse.Content.ReadFromJsonAsync<TwoFactorResponse>();
        twoFactor.Should().NotBeNull();
        twoFactor.SharedKey.Should().NotBeNullOrWhiteSpace();
        twoFactor.IsTwoFactorEnabled.Should().BeFalse();
    }

    [Theory]
    [DataInline]
    public async Task Refresh_Should_Preserve_Exact_Configured_Mfa_Claim_And_Discard_Unrelated_Same_Type_Claims(DrnTestContext context)
    {
        const string customClaimType = "permission";
        const string mfaClaimValue = "mfa";
        const string unrelatedClaimValue = "admin";

        var application = context.ApplicationContext.CreateApplication<SampleProgram>(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new MfaClaimConfig(customClaimType, mfaClaimValue));
            }));

        await context.ContainerContext.BindExternalDependenciesAsync();
        application.Server.PreserveExecutionContext = true;
        using var client = application.CreateClient();

        var identity = Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(
            identity.LoginController.Login.RoutePattern!,
            identity.RegisterController.Register.RoutePattern!,
            identity.ManagementController.TwoFactorAuth.RoutePattern!);
        var refreshUrl = identity.LoginController.Refresh.RoutePattern!;

        var credentials = CredentialsProvider.GenerateCredentials();
        var registerRequest = new RegisterRequest
        {
            Email = $"{credentials.Username}@example.com",
            Password = credentials.Password
        };
        await AuthenticationHelper.RegisterUserAsync(client, registerRequest, endpoints);

        // Retrieve registered user and construct a refresh token containing both the custom MFA claim
        // and an unrelated claim of the same type (permission: admin).
        var userManager = application.Services.GetRequiredService<UserManager<SampleUser>>();
        var signInManager = application.Services.GetRequiredService<SignInManager<SampleUser>>();
        var bearerOptionsMonitor = application.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>();
        var bearerOptions = bearerOptionsMonitor.Get(IdentityConstants.BearerScheme);

        var user = await userManager.FindByEmailAsync(registerRequest.Email);
        user.Should().NotBeNull();

        var principal = await signInManager.CreateUserPrincipalAsync(user!);
        const string unauthenticatedAmr = "unauthenticated_amr";
        const string unauthenticatedMfa = "unauthenticated_mfa";
        if (principal.Identity is ClaimsIdentity claimsIdentity)
        {
            claimsIdentity.AddClaim(new Claim(customClaimType, mfaClaimValue));
            claimsIdentity.AddClaim(new Claim(customClaimType, unrelatedClaimValue));
        }

        principal.AddIdentity(new ClaimsIdentity([
            new Claim(ClaimConventions.AuthenticationMethodReference, unauthenticatedAmr),
            new Claim(customClaimType, unauthenticatedMfa)
        ]));

        var refreshTicket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        }, $"{IdentityConstants.BearerScheme}:RefreshToken");
        var refreshToken = bearerOptions.RefreshTokenProtector.Protect(refreshTicket);

        // Execute /refresh request
        using var refreshResponse = await client.PostAsJsonAsync(refreshUrl, new RefreshRequest
        {
            RefreshToken = refreshToken
        });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedToken = await refreshResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        refreshedToken.Should().NotBeNull();
        refreshedToken!.AccessToken.Should().NotBeNullOrWhiteSpace();

        // Inspect the regenerated access token principal
        var accessTokenTicket = bearerOptions.BearerTokenProtector.Unprotect(refreshedToken.AccessToken);
        accessTokenTicket.Should().NotBeNull();
        var refreshedPrincipal = accessTokenTicket!.Principal;

        // The exact configured MFA claim (permission: mfa) must be preserved
        refreshedPrincipal.HasClaim(customClaimType, mfaClaimValue).Should().BeTrue();

        // Unrelated claims sharing the same claim type (permission: admin) must NOT be copied from the refresh token
        refreshedPrincipal.HasClaim(customClaimType, unrelatedClaimValue).Should().BeFalse();

        // Claims originating from unauthenticated identities must NOT be copied from the refresh token
        refreshedPrincipal.HasClaim(ClaimConventions.AuthenticationMethodReference, unauthenticatedAmr).Should().BeFalse();
        refreshedPrincipal.HasClaim(customClaimType, unauthenticatedMfa).Should().BeFalse();
    }

    [Theory]
    [DataInline]
    public async Task Refresh_Should_Not_Promote_Unauthenticated_Mfa_Claim_To_Mfa_Complete(DrnTestContext context)
    {
        var application = context.ApplicationContext.CreateApplication<SampleProgram>();
        await context.ContainerContext.BindExternalDependenciesAsync();
        application.Server.PreserveExecutionContext = true;
        using var client = application.CreateClient();

        var identity = Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(
            identity.LoginController.Login.RoutePattern!,
            identity.RegisterController.Register.RoutePattern!,
            identity.ManagementController.TwoFactorAuth.RoutePattern!);
        var refreshUrl = identity.LoginController.Refresh.RoutePattern!;
        var protectedUrl = identity.ManagementController.GetInfo.RoutePattern!;

        var credentials = CredentialsProvider.GenerateCredentials();
        var registerRequest = new RegisterRequest
        {
            Email = $"{credentials.Username}@example.com",
            Password = credentials.Password
        };
        await AuthenticationHelper.RegisterUserAsync(client, registerRequest, endpoints);

        var userManager = application.Services.GetRequiredService<UserManager<SampleUser>>();
        var signInManager = application.Services.GetRequiredService<SignInManager<SampleUser>>();
        var bearerOptionsMonitor = application.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>();
        var bearerOptions = bearerOptionsMonitor.Get(IdentityConstants.BearerScheme);

        var user = await userManager.FindByEmailAsync(registerRequest.Email);
        user.Should().NotBeNull();

        // Create principal with password-only authenticated identity, and attach an unauthenticated identity containing amr=mfa
        var principal = await signInManager.CreateUserPrincipalAsync(user!);
        principal.AddIdentity(new ClaimsIdentity([
            new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr)
        ]));

        var refreshTicket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        }, $"{IdentityConstants.BearerScheme}:RefreshToken");
        var refreshToken = bearerOptions.RefreshTokenProtector.Protect(refreshTicket);

        using var refreshResponse = await client.PostAsJsonAsync(refreshUrl, new RefreshRequest
        {
            RefreshToken = refreshToken
        });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedToken = await refreshResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        refreshedToken.Should().NotBeNull();
        refreshedToken!.AccessToken.Should().NotBeNullOrWhiteSpace();

        var accessTokenTicket = bearerOptions.BearerTokenProtector.Unprotect(refreshedToken.AccessToken);
        accessTokenTicket.Should().NotBeNull();
        var refreshedPrincipal = accessTokenTicket!.Principal;

        // The unauthenticated AMR claim must NOT be promoted to the refreshed access token
        refreshedPrincipal.HasClaim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr).Should().BeFalse();

        // Accessing an MFA-enforced endpoint with the refreshed token must be rejected with 403 Forbidden
        using var protectedRequest = new HttpRequestMessage(HttpMethod.Get, protectedUrl);
        protectedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken.AccessToken);
        using var protectedResponse = await client.SendAsync(protectedRequest);
        protectedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpClient> CreateOptionalMfaClientAsync(DrnTestContext context)
    {
        var application = context.ApplicationContext.CreateApplication<SampleProgram>(builder =>
            builder.ConfigureServices(services => services.PostConfigure<AuthorizationOptions>(options =>
            {
                var authenticatedUserPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.DefaultPolicy = authenticatedUserPolicy;
                options.FallbackPolicy = authenticatedUserPolicy;
            })));

        await context.ContainerContext.BindExternalDependenciesAsync();
        application.Server.PreserveExecutionContext = true;
        return application.CreateClient();
    }

    private static async Task AssertPipelineStatusAsync(
        HttpClient client,
        string? credential,
        HttpStatusCode expectedStatus,
        string path = MfaPipelineTestValues.ProtectedPath)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (credential != null)
            request.Headers.Add(MfaPipelineTestValues.CredentialHeader, credential);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(expectedStatus);
    }

    private static async Task<string?> GetLoginFailureAsync(HttpClient client, string loginUrl, string email, string password)
    {
        using var response = await client.PostAsJsonAsync(loginUrl, new LoginRequest
        {
            Email = email,
            Password = password
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        return problem!.Detail;
    }
}
