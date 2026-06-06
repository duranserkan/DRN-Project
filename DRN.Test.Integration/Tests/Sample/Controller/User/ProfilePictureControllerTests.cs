using System.Net;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Users;
using Sample.Hosted;
using Sample.Hosted.Helpers;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Controller.User;

public class ProfilePictureControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task Profile_Picture_Should_Return_Image_With_User_Id_And_Reject_No_User_Id_Route(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        var identity = Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(identity.LoginController.Login.RoutePattern!, identity.RegisterController.Register.RoutePattern!);
        var currentCredentials = CredentialsProvider.GenerateCredentials();
        var currentUser = await AuthenticationHelper.AuthenticateClientAsync(client, currentCredentials, endpoints);
        using var scope = context.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SampleUser>>();
        var user = await userManager.FindByEmailAsync(currentUser.Email);
        user.Should().NotBeNull();

        var profilePicturePath = $"{Get.Endpoint.User.PP.ControllerRoute}/{user!.Id}";

        var ownResponse = await client.GetAsync(profilePicturePath);
        ownResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        ownResponse.Headers.CacheControl.Should().NotBeNull();
        ownResponse.Headers.CacheControl!.Private.Should().BeTrue();
        ownResponse.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(31536000));

        var noUserIdResponse = await client.GetAsync(Get.Endpoint.User.PP.ControllerRoute);
        noUserIdResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
