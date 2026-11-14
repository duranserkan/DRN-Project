using System.Net;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Test.Integration.Tests.Sample.Pages.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Sample.Domain.Users;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Pages.User;

public class LoginLogoutPageTests
{
    [Theory]
    [DataInline]
    public async Task Login_And_Logout_Should_Update_Rendered_Navigation_And_Protected_Page_Access(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(
            clientOptions: new()
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        using var scope = context.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<SampleUser>>();
        var (_, credentials, key) = await PageTestHelper.CreateMfaUserAsync(manager);

        // HomeAnonymous declares @page "/", so its page name is not its request URL.
        using var anonymousHome = await client.GetAsync(Get.Page.Root.Root);
        anonymousHome.StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymousHome.Content.ReadAsStringAsync()).Should()
            .Contain($"href=\"{Get.Page.User.Login}\"")
            .And.NotContain($"href=\"{Get.Page.User.Logout}\"");

        using var anonymousProfile = await client.GetAsync(Get.Page.User.Profile.Details);
        anonymousProfile.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        var loginLocation = anonymousProfile.Headers.Location!.OriginalString;
        loginLocation.Should().StartWith(Get.Page.User.Login + "?");
        QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, loginLocation).Query)["ReturnUrl"].ToString()
            .Should().Be(Get.Page.User.Profile.Details);

        var token = await PageTestHelper.GetAntiforgeryTokenAsync(client, Get.Page.User.Login);
        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = credentials.EmailAddress,
            ["Input.Password"] = credentials.Password,
            ["Input.ReturnUrl"] = Get.Page.User.Profile.Details,
            ["__RequestVerificationToken"] = token
        });
        using var loginResponse = await client.PostAsync(Get.Page.User.Login, loginForm);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        var challengePath = loginResponse.Headers.Location!.OriginalString;
        challengePath.Should().StartWith(Get.Page.User.LoginWith2Fa + "?");

        token = await PageTestHelper.GetAntiforgeryTokenAsync(client, challengePath);
        using var challengeForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Login2FaModel.TwoFactorCode"] = TotpUtils.GenerateTotpCode(key),
            ["__RequestVerificationToken"] = token
        });
        using var challengeResponse = await client.PostAsync(challengePath, challengeForm);
        challengeResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        challengeResponse.Headers.Location!.OriginalString.Should().Be(Get.Page.User.Profile.Details);

        using var profile = await client.GetAsync(challengeResponse.Headers.Location);
        profile.StatusCode.Should().Be(HttpStatusCode.OK);
        (await profile.Content.ReadAsStringAsync()).Should()
            .Contain(credentials.EmailAddress)
            .And.Contain($"href=\"{Get.Page.User.Logout}\"")
            .And.NotContain($"href=\"{Get.Page.User.Login}\"");

        using var logoutPage = await client.GetAsync(Get.Page.User.Logout);
        logoutPage.StatusCode.Should().Be(HttpStatusCode.OK);
        (await logoutPage.Content.ReadAsStringAsync()).Should().Contain("Are you sure you want to log out?");

        using var unverifiedLogout = await client.PostAsync(Get.Page.User.Logout, new FormUrlEncodedContent([]));
        unverifiedLogout.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var stillAuthenticated = await client.GetAsync(Get.Page.User.Profile.Details);
        stillAuthenticated.StatusCode.Should().Be(HttpStatusCode.OK,
            "viewing the logout confirmation and posting without an antiforgery token must preserve the session");

        // The authenticated principal needs a fresh token after completing MFA.
        token = await PageTestHelper.GetAntiforgeryTokenAsync(client, Get.Page.User.Logout);
        using var logoutForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        using var logoutResponse = await client.PostAsync(Get.Page.User.Logout, logoutForm);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        logoutResponse.Headers.Location!.OriginalString.Should().Be(Get.Page.Root.Root);

        using var loggedOutHome = await client.GetAsync(logoutResponse.Headers.Location);
        loggedOutHome.StatusCode.Should().Be(HttpStatusCode.OK);
        (await loggedOutHome.Content.ReadAsStringAsync()).Should()
            .Contain($"href=\"{Get.Page.User.Login}\"")
            .And.NotContain($"href=\"{Get.Page.User.Logout}\"");
        using var loggedOutProfile = await client.GetAsync(Get.Page.User.Profile.Details);
        loggedOutProfile.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        loggedOutProfile.Headers.Location!.OriginalString.Should().Be(loginLocation);
        using var anonymousLogout = await client.GetAsync(Get.Page.User.Logout);
        anonymousLogout.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        anonymousLogout.Headers.Location!.OriginalString.Should().Be(Get.Page.Root.Root);
    }
}
