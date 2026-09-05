using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Http;
using Sample.Hosted.Controllers.User;
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
using Sample.Hosted.Pages.User.Management;

namespace DRN.Test.Integration.Tests.Framework.Hosting.Auth;

public class BearerMfaEnforcementTests
{
    [Theory]
    [DataInline]
    public async Task Management_Must_Not_Use_Another_Accounts_Ambient_Mfa(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        var app = context.ApplicationContext.GetCreatedApplication<SampleProgram>()!;
        using var scope = app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<SampleUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<SampleUser>>();
        var email = $"mfa-boundary-{Guid.NewGuid():N}@example.com";
        var account = new SampleUser { UserName = email, Email = email };
        (await manager.CreateAsync(account)).Succeeded.Should().BeTrue();
        (await manager.ResetAuthenticatorKeyAsync(account)).Succeeded.Should().BeTrue();
        (await manager.SetTwoFactorEnabledAsync(account, true)).Succeeded.Should().BeTrue();
        var key = await manager.GetAuthenticatorKeyAsync(account);
        var target = await signIn.CreateUserPrincipalAsync(account);
        var ambient = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "another-account"), new Claim("amr", "mfa")
        ], IdentityConstants.ApplicationScheme));
        ScopeContext.InitializeForTest(scope.ServiceProvider, scopedUser: ScopedUser.FromClaimsPrincipal(ambient));
        var controller = new SampleIdentityManagementController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = target }
            }
        };

        foreach (var request in new[]
                 {
                     new TwoFactorRequest(), new TwoFactorRequest { Enable = false },
                     new TwoFactorRequest { ResetSharedKey = true }, new TwoFactorRequest { ResetRecoveryCodes = true }
                 })
        {
            var result = await controller.TwoFactorAuth(request);
            result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            (await manager.GetTwoFactorEnabledAsync(account)).Should().BeTrue();
            (await manager.GetAuthenticatorKeyAsync(account)).Should().Be(key);
            (await manager.CountRecoveryCodesAsync(account)).Should().Be(0);
        }

        var page = new EnableAuthenticator(signIn, manager)
        {
            PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = target }
            }
        };
        (await page.OnGetAsync()).Should().BeOfType<ForbidResult>();
        (await page.OnPostVerifyAsync()).Should().BeOfType<ForbidResult>();
        page.SharedKey.Should().BeEmpty();
        (await manager.GetAuthenticatorKeyAsync(account)).Should().Be(key);
    }

    [Theory]
    [DataInline]
    public async Task Selected_Credentials_And_Mfa_Configuration_Are_Isolated_Per_Host(DrnTestContext context)
    {
        using var userClient = await context.ApplicationContext.CreateClientAsync<NonDefaultExemptSchemeTestProgram>();
        using var externalClient = await context.ApplicationContext.CreateClientAsync<MfaExemptionPipelineTestProgram>();

        using var password = new HttpRequestMessage(HttpMethod.Get, NonDefaultExemptValues.CookieRoleProtectedPath);
        password.Headers.Add(NonDefaultExemptValues.CookieHeader, "password");
        password.Headers.Add(NonDefaultExemptValues.ApiKeyHeader, "valid-unselected-key");
        using var rejected = await userClient.SendAsync(password);
        rejected.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var keys = new HttpRequestMessage(HttpMethod.Get, NonDefaultExemptValues.SecondApiKeyProtectedPath);
        keys.Headers.Add(NonDefaultExemptValues.ApiKeyHeader, "first");
        keys.Headers.Add(NonDefaultExemptValues.SecondApiKeyHeader, "second");
        keys.Headers.Add(NonDefaultExemptValues.CookieHeader, "password");
        using var selected = await userClient.SendAsync(keys);
        selected.StatusCode.Should().Be(HttpStatusCode.OK);
        (await selected.Content.ReadAsStringAsync()).Should().Be(NonDefaultExemptValues.SecondApiKeyScheme);

        using var missing = new HttpRequestMessage(HttpMethod.Get, NonDefaultExemptValues.SecondApiKeyProtectedPath);
        missing.Headers.Add(NonDefaultExemptValues.ApiKeyHeader, "first");
        using var missingResult = await userClient.SendAsync(missing);
        missingResult.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var external = new HttpRequestMessage(HttpMethod.Get, MfaPipelineTestValues.NamedSchemeProtectedPath);
        external.Headers.Add(MfaPipelineTestValues.CredentialHeader, MfaPipelineTestValues.CompletedCredential);
        using var externalResult = await externalClient.SendAsync(external);
        externalResult.StatusCode.Should().Be(HttpStatusCode.OK);

        using var identity = new HttpRequestMessage(HttpMethod.Get, NonDefaultExemptValues.CookieRoleProtectedPath);
        identity.Headers.Add(NonDefaultExemptValues.CookieHeader, "completed");
        using var identityResult = await userClient.SendAsync(identity);
        identityResult.StatusCode.Should().Be(HttpStatusCode.OK);
    }

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
    [DataInline(MfaPipelineTestValues.ProtectedPath, null, HttpStatusCode.Unauthorized)]
    [DataInline(MfaPipelineTestValues.ProtectedPath, MfaPipelineTestValues.InvalidCredential, HttpStatusCode.Unauthorized)]
    [DataInline(MfaPipelineTestValues.ProtectedPath, MfaPipelineTestValues.PasswordCredential, HttpStatusCode.OK)]
    [DataInline(MfaPipelineTestValues.ProtectedPath, MfaPipelineTestValues.SetupCredential, HttpStatusCode.Forbidden)]
    [DataInline(MfaPipelineTestValues.ProtectedPath, MfaPipelineTestValues.SetupAndCompletedCredential, HttpStatusCode.Forbidden)]
    [DataInline(MfaPipelineTestValues.ProtectedPath, MfaPipelineTestValues.CompletedCredential, HttpStatusCode.OK)]
    [DataInline(MfaPipelineTestValues.RoleProtectedPath, null, HttpStatusCode.Unauthorized)]
    [DataInline(MfaPipelineTestValues.RoleProtectedPath, MfaPipelineTestValues.InvalidCredential, HttpStatusCode.Unauthorized)]
    [DataInline(MfaPipelineTestValues.RoleProtectedPath, MfaPipelineTestValues.PasswordCredential, HttpStatusCode.OK)]
    [DataInline(MfaPipelineTestValues.RoleProtectedPath, MfaPipelineTestValues.SetupCredential, HttpStatusCode.Forbidden)]
    [DataInline(MfaPipelineTestValues.RoleProtectedPath, MfaPipelineTestValues.SetupAndCompletedCredential, HttpStatusCode.Forbidden)]
    [DataInline(MfaPipelineTestValues.RoleProtectedPath, MfaPipelineTestValues.CompletedCredential, HttpStatusCode.OK)]
    public async Task Default_And_Role_Policies_Should_Enforce_Mfa(
        DrnTestContext context, string path, string? credential, HttpStatusCode expectedStatus)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<MfaExemptionPipelineTestProgram>();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (credential != null)
            request.Headers.Add(MfaPipelineTestValues.CredentialHeader, credential);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(expectedStatus);
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
    [DataInline(NonDefaultExemptValues.CookieRoleProtectedPath, false, true, HttpStatusCode.Unauthorized)]
    [DataInline(NonDefaultExemptValues.CookieRoleProtectedPath, true, false, HttpStatusCode.OK)]
    [DataInline(NonDefaultExemptValues.ApiKeyProtectedPath, false, true, HttpStatusCode.OK)]
    [DataInline(NonDefaultExemptValues.ApiKeyProtectedPath, true, true, HttpStatusCode.OK)]
    public async Task Non_Default_Exempt_Scheme_Must_Not_Authenticate_Unrelated_Endpoints(
        DrnTestContext context, string path, bool sendCookie, bool sendApiKey, HttpStatusCode expectedStatus)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<NonDefaultExemptSchemeTestProgram>();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (sendCookie)
            request.Headers.Add(NonDefaultExemptValues.CookieHeader, "cookie-valid");
        if (sendApiKey)
            request.Headers.Add(NonDefaultExemptValues.ApiKeyHeader, "api-key-valid");

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(expectedStatus);
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
        var principal = await RegisterPrincipalAsync(client, application.Services, endpoints);
        var bearerOptions = application.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
            .Get(IdentityConstants.BearerScheme);
        const string unauthenticatedAmr = "unauthenticated_amr";
        const string unauthenticatedMfa = "unauthenticated_mfa";
        principal.Identity.Should().BeOfType<ClaimsIdentity>();
        var claimsIdentity = (ClaimsIdentity)principal.Identity!;
        claimsIdentity.AddClaim(new Claim(customClaimType, mfaClaimValue));
        claimsIdentity.AddClaim(new Claim(customClaimType, unrelatedClaimValue));
        claimsIdentity.AddClaim(new Claim("auth_time", "1700000000", ClaimValueTypes.Integer64, "trusted-issuer"));

        principal.AddIdentity(new ClaimsIdentity([
            new Claim(ClaimConventions.AuthenticationMethodReference, unauthenticatedAmr),
            new Claim(customClaimType, unauthenticatedMfa)
        ]));

        var refreshToken = CreateRefreshToken(principal, bearerOptions);

        // Execute /refresh request
        using var refreshResponse = await client.PostAsJsonAsync(refreshUrl, new RefreshRequest
        {
            RefreshToken = refreshToken
        });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedToken = await refreshResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        refreshedToken.Should().NotBeNull();
        refreshedToken.AccessToken.Should().NotBeNullOrWhiteSpace();

        // Inspect the regenerated access token principal
        var accessTokenTicket = bearerOptions.BearerTokenProtector.Unprotect(refreshedToken.AccessToken);
        accessTokenTicket.Should().NotBeNull();
        var refreshedPrincipal = accessTokenTicket.Principal;

        // The exact configured MFA claim (permission: mfa) must be preserved
        refreshedPrincipal.HasClaim(customClaimType, mfaClaimValue).Should().BeTrue();
        var authenticationTime = refreshedPrincipal.FindAll("auth_time").Should().ContainSingle().Which;
        authenticationTime.Value.Should().Be("1700000000");
        authenticationTime.Issuer.Should().Be("trusted-issuer");
        var renewedRefreshTicket = bearerOptions.RefreshTokenProtector.Unprotect(refreshedToken.RefreshToken);
        renewedRefreshTicket.Should().NotBeNull();
        renewedRefreshTicket.Principal.FindAll("auth_time").Should().ContainSingle().Which.Value.Should().Be("1700000000");

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
        var principal = await RegisterPrincipalAsync(client, application.Services, endpoints);
        var bearerOptions = application.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
            .Get(IdentityConstants.BearerScheme);
        principal.AddIdentity(new ClaimsIdentity([
            new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr)
        ]));

        var refreshToken = CreateRefreshToken(principal, bearerOptions);

        using var refreshResponse = await client.PostAsJsonAsync(refreshUrl, new RefreshRequest
        {
            RefreshToken = refreshToken
        });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedToken = await refreshResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        refreshedToken.Should().NotBeNull();
        refreshedToken.AccessToken.Should().NotBeNullOrWhiteSpace();

        var accessTokenTicket = bearerOptions.BearerTokenProtector.Unprotect(refreshedToken.AccessToken);
        accessTokenTicket.Should().NotBeNull();
        var refreshedPrincipal = accessTokenTicket.Principal;

        // The unauthenticated AMR claim must NOT be promoted to the refreshed access token
        refreshedPrincipal.HasClaim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr).Should().BeFalse();

        // Accessing an MFA-enforced endpoint with the refreshed token must be rejected with 403 Forbidden
        using var protectedRequest = new HttpRequestMessage(HttpMethod.Get, protectedUrl);
        protectedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken.AccessToken);
        using var protectedResponse = await client.SendAsync(protectedRequest);
        protectedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<ClaimsPrincipal> RegisterPrincipalAsync(
        HttpClient client, IServiceProvider services, AuthenticationEndpoints endpoints)
    {
        var credentials = CredentialsProvider.GenerateCredentials();
        var registration = new RegisterRequest
        {
            Email = $"{credentials.Username}@example.com",
            Password = credentials.Password
        };
        await AuthenticationHelper.RegisterUserAsync(client, registration, endpoints);

        var userManager = services.GetRequiredService<UserManager<SampleUser>>();
        var user = await userManager.FindByEmailAsync(registration.Email);
        user.Should().NotBeNull();
        return await services.GetRequiredService<SignInManager<SampleUser>>().CreateUserPrincipalAsync(user);
    }

    private static string CreateRefreshToken(ClaimsPrincipal principal, BearerTokenOptions options)
    {
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        }, $"{IdentityConstants.BearerScheme}:RefreshToken");
        return options.RefreshTokenProtector.Protect(ticket);
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
        return problem.Detail;
    }
}
