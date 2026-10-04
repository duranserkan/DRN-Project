using System.Net;
using System.Security.Claims;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Sample.Domain.Identity;
using Sample.Domain.Users;
using Sample.Hosted;
using Sample.Hosted.Helpers;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Pages.User;

public class LoginPageTests
{
    [Theory]
    [DataInline("")]
    [DataInline("/gateway")]
    public async Task Mfa_Login_Should_Preserve_Public_ReturnUrl_And_Render_Prefixed_Links(
        DrnTestContext context, string pathBase)
    {
        var application = context.ApplicationContext.CreateApplication<SampleProgram>(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new PathBaseStartupFilter(pathBase))));
        await context.ContainerContext.BindExternalDependenciesAsync();
        application.Server.PreserveExecutionContext = true;
        using var client = application.CreateClient(new() { AllowAutoRedirect = false });
        using var scope = application.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<SampleUser>>();
        var credentials = CredentialsProvider.GenerateCredentials();
        var email = $"{credentials.Username}@example.com";
        var user = new SampleUser
        {
            UserName = email, Email = email, EmailConfirmed = true, PhoneNumberConfirmed = true
        };
        (await manager.CreateAsync(user, credentials.Password)).Succeeded.Should().BeTrue();
        (await manager.AddClaimAsync(user, new Claim(manager.Options.ClaimsIdentity.RoleClaimType, UserRoles.SystemAdmin)))
            .Succeeded.Should().BeTrue();
        (await manager.ResetAuthenticatorKeyAsync(user)).Succeeded.Should().BeTrue();
        (await manager.SetTwoFactorEnabledAsync(user, true)).Succeeded.Should().BeTrue();
        var key = await manager.GetAuthenticatorKeyAsync(user);
        key.Should().NotBeNullOrEmpty();

        var loginPath = pathBase + Get.Page.User.Login;
        var returnUrl = $"{pathBase}{Get.Page.Test.Htmx}?tag=one&tag=two&next=%2Fitems%3Fx%3D1#details";
        var token = await GetAntiforgeryTokenAsync(client, loginPath);
        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = credentials.Password,
            ["Input.ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token
        });
        using var loginResponse = await client.PostAsync(loginPath, loginForm);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        var challengePath = loginResponse.Headers.Location!.OriginalString;
        challengePath.Should().StartWith(pathBase + Get.Page.User.LoginWith2Fa + "?");

        token = await GetAntiforgeryTokenAsync(client, challengePath);
        using var challengeForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Login2FaModel.TwoFactorCode"] = TotpUtils.GenerateTotpCode(key),
            ["__RequestVerificationToken"] = token
        });
        using var challengeResponse = await client.PostAsync(challengePath, challengeForm);
        challengeResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        challengeResponse.Headers.Location!.OriginalString.Should().Be(returnUrl);

        using var landingResponse = await client.GetAsync(challengeResponse.Headers.Location);
        landingResponse.EnsureSuccessStatusCode();
        var html = await landingResponse.Content.ReadAsStringAsync();
        html.Should().Contain($"name=\"drn-app-base\" content=\"{pathBase}/\"")
            .And.Contain($"href=\"{pathBase}{Get.Page.Root.Home}\"")
            .And.Contain($"hx-post=\"{pathBase}{Get.Page.Test.Htmx}?handler=Auto\"")
            .And.Contain($"src=\"{pathBase}{Get.Endpoint.User.PP.ControllerRoute}/{user.Id}?v=")
            .And.NotContain("hx-post=\"~/");
    }

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

        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);
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

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string? pagePath = null)
    {
        var loginPage = await client.GetStringAsync(pagePath ?? Get.Page.User.Login);
        const string tokenName = "__RequestVerificationToken";
        var nameIndex = loginPage.IndexOf($"name=\"{tokenName}\"", StringComparison.Ordinal);
        nameIndex.Should().BeGreaterThanOrEqualTo(0);

        const string valuePrefix = "value=\"";
        var valueIndex = loginPage.IndexOf(valuePrefix, nameIndex, StringComparison.Ordinal);
        valueIndex.Should().BeGreaterThanOrEqualTo(0);
        valueIndex += valuePrefix.Length;

        var valueEndIndex = loginPage.IndexOf('"', valueIndex);
        valueEndIndex.Should().BeGreaterThan(valueIndex);

        var token = loginPage[valueIndex..valueEndIndex];
        token.Should().NotBeNullOrWhiteSpace();

        return token;
    }

    private sealed class PathBaseStartupFilter(string pathBase) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            if (pathBase.Length > 0)
                app.UsePathBase(pathBase);
            next(app);
        };
    }
}
