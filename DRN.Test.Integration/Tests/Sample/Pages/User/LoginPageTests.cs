using System.Net;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Sample.Domain.Users;
using Sample.Hosted;
using Sample.Hosted.Helpers;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;
using DRN.Test.Integration.Tests.Sample.Pages.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Pages.User;

public class LoginPageTests
{
    [Theory]
    [DataInline]
    public async Task Login_Page_Should_Record_Failed_Password_Attempts_For_Lockout(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        var identity = Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(identity.LoginController.Login.RoutePattern!, identity.RegisterController.Register.RoutePattern!);
        var registerRequest = new RegisterRequest
        {
            Email = $"lockout-{Guid.NewGuid():N}@example.com",
            Password = CredentialsProvider.Credentials.Password
        };
        await AuthenticationHelper.RegisterUserAsync(client, registerRequest, endpoints);

        var antiforgeryToken = await PageTestHelper.GetAntiforgeryTokenAsync(client, Get.Page.User.Login);
        for (var i = 0; i < 3; i++)
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = registerRequest.Email,
                ["Input.Password"] = $"{registerRequest.Password}-wrong",
                ["__RequestVerificationToken"] = antiforgeryToken
            });
            using var response = await client.PostAsync(Get.Page.User.Login, form);
        }

        using var scope = context.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SampleUser>>();
        var user = await userManager.FindByEmailAsync(registerRequest.Email);

        user.Should().NotBeNull();
        (await userManager.IsLockedOutAsync(user)).Should().BeTrue();
    }

    [Theory]
    [DataInline(false)]
    [DataInline(true)]
    public async Task Password_Login_Should_Preserve_Mfa_Failures_Until_Lockout_Or_Success(
        DrnTestContext context, bool completeMfa)
    {
        var application = context.ApplicationContext.CreateApplication<SampleProgram>();
        await context.ContainerContext.BindExternalDependenciesAsync();
        application.Server.PreserveExecutionContext = true;
        using var client = application.CreateClient(new() { AllowAutoRedirect = false });
        using var scope = application.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<SampleUser>>();
        var (user, credentials, authenticatorKey) = await PageTestHelper.CreateMfaUserAsync(manager);
        var email = credentials.EmailAddress;
        (await manager.GetLockoutEnabledAsync(user)).Should().BeTrue();
        var failureLimit = manager.Options.Lockout.MaxFailedAccessAttempts;
        failureLimit.Should().BeGreaterThan(1);
        var token = await PageTestHelper.GetAntiforgeryTokenAsync(client, Get.Page.User.Login);
        var challengePath = Get.Page.User.LoginWith2Fa;
        var invalidAttempts = completeMfa ? failureLimit - 1 : failureLimit;

        for (var attempt = 1; attempt <= invalidAttempts; attempt++)
        {
            using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = credentials.Password,
                ["__RequestVerificationToken"] = token
            });
            using var loginResponse = await client.PostAsync(Get.Page.User.Login, loginForm);
            loginResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
            challengePath = loginResponse.Headers.Location!.OriginalString;
            challengePath.Should().StartWith(Get.Page.User.LoginWith2Fa);
            (await ReadFailureCountAsync(application.Services, email)).Should().Be(attempt - 1);

            token = await PageTestHelper.GetAntiforgeryTokenAsync(client, challengePath);
            using var challengeForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                // Pass the page's length validation without risking a valid TOTP collision.
                ["Login2FaModel.TwoFactorCode"] = "abcdef",
                ["__RequestVerificationToken"] = token
            });
            using var challengeResponse = await client.PostAsync(challengePath, challengeForm);
            if (attempt < failureLimit)
            {
                challengeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
                (await challengeResponse.Content.ReadAsStringAsync()).Should().Contain("Invalid authenticator code.");
                (await ReadFailureCountAsync(application.Services, email)).Should().Be(attempt);
            }
            else
            {
                challengeResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
                challengeResponse.Headers.Location!.OriginalString.Should().Be(Get.Page.User.Logout);
            }
        }

        if (completeMfa)
        {
            using var successForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Login2FaModel.TwoFactorCode"] = TotpUtils.GenerateTotpCode(authenticatorKey),
            });
            using var successResponse = await client.PostAsync(challengePath, successForm);
            successResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
            successResponse.Headers.Location!.OriginalString.Should().Be(Get.Page.Root.Home);
            (await ReadFailureCountAsync(application.Services, email)).Should().Be(0);
            return;
        }

        // Keep the pending principal and antiforgery token paired by not following Logout.
        using var lockedOutForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = credentials.Password,
            ["__RequestVerificationToken"] = token
        });
        using var lockedOutResponse = await client.PostAsync(Get.Page.User.Login, lockedOutForm);
        lockedOutResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        lockedOutResponse.Headers.Location!.OriginalString.Should().Be(Get.Page.User.Lockout);
    }

    private static async Task<int> ReadFailureCountAsync(IServiceProvider services, string email)
    {
        // A fresh scope reads persisted state instead of the setup context's tracked user.
        using var scope = services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<SampleUser>>();
        var user = await manager.FindByEmailAsync(email);
        user.Should().NotBeNull();
        return await manager.GetAccessFailedCountAsync(user);
    }
}
