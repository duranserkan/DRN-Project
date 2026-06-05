using System.Net;
using Sample.Domain.Users;
using Sample.Hosted;
using Sample.Hosted.Helpers;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Controller.User;

public class ProfilePictureControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task Profile_Picture_Should_Return_Current_User_Image_Without_User_Id(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        var identity = Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(identity.LoginController.Login.RoutePattern!, identity.RegisterController.Register.RoutePattern!, typeof(SampleUser));
        var currentCredentials = CredentialsProvider.GenerateCredentials();
        await AuthenticationHelper.AuthenticateClientAsync(context, client, currentCredentials, endpoints);

        var profilePicturePath = Get.Endpoint.User.PP.Get.Path();

        var ownResponse = await client.GetAsync(profilePicturePath);
        ownResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var legacyUserIdResponse = await client.GetAsync($"{profilePicturePath}/{Guid.NewGuid():N}");
        legacyUserIdResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
