using System.Net;
using System.Net.Http.Json;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Controller;

public class ClientErrorControllerTests
{
    [Theory]
    [DataInline]
    public async Task Reports_Should_Require_Authentication_And_Limit_Each_User(DrnTestContext context)
    {
        context.AddToConfiguration("DrnAppFeatures:DrnRateLimit:Disabled", "false");
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        const string path = "/Api/Sample/ClientError/Report";
        var payload = new { message = "Browser test error", clientErrorId = new string('a', 32) };
        using var anonymous = await client.PostAsJsonAsync(path, payload);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var identity = Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(identity.LoginController.Login.RoutePattern!,
            identity.RegisterController.Register.RoutePattern!, identity.ManagementController.TwoFactorAuth.RoutePattern!);
        await AuthenticationHelper.AuthenticateClientAsync(client, CredentialsProvider.GenerateCredentials(), endpoints);
        for (var index = 0; index < 5; index++)
        {
            using var accepted = await client.PostAsJsonAsync(path, payload);
            accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var rejected = await client.PostAsJsonAsync(path, payload);
        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        client.DefaultRequestHeaders.Authorization = null;
        await AuthenticationHelper.AuthenticateClientAsync(client, CredentialsProvider.GenerateCredentials(), endpoints);
        using var otherUser = await client.PostAsJsonAsync(path, payload);
        otherUser.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
