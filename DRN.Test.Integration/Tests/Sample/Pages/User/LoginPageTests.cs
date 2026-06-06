using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Sample.Domain.Users;
using Sample.Hosted;
using Sample.Hosted.Helpers;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Pages.User;

public class LoginPageTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task Login_Page_Should_Record_Failed_Password_Attempts_For_Lockout(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
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
        (await userManager.IsLockedOutAsync(user!)).Should().BeTrue();
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var loginPage = await client.GetStringAsync(Get.Page.User.Login);
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
}
