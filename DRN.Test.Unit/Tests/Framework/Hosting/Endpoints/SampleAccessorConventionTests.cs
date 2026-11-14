using Sample.Hosted.Helpers.EndpointFor;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Endpoints;

public class SampleAccessorConventionTests
{
    [Fact]
    public void Sample_Accessors_Should_Preserve_Paths_Types_And_Legacy_Routes()
    {
        var endpoint = new SampleEndpointFor();

        endpoint.QA.Should().BeOfType<QaApiFor>();
        endpoint.User.Identity.Should().BeOfType<UserIdentityFor>();
        endpoint.QA.Tag.Should().BeOfType<TagFor>().Which.ControllerRoute.Should().Be("/Api/QA/Tag");
        endpoint.QA.Category.Should().BeOfType<CategoryFor>().Which.ControllerRoute.Should().Be("/Api/QA/Category");
        endpoint.Sample.Private.Should().BeOfType<PrivateFor>().Which.ControllerRoute.Should().Be("/Api/Sample/Private");
        endpoint.Sample.WeatherForecast.Should().BeOfType<WeatherForecastFor>().Which.ControllerRoute.Should().Be("/Api/Sample/WeatherForecast");
        endpoint.Sample.Exception.Should().BeOfType<ExceptionControllerFor>().Which.ControllerRoute.Should().Be("/Api/Sample/Exception");
        endpoint.User.Identity.RegisterController.Should().BeOfType<UserIdentityRegisterFor>().Which.ControllerRoute.Should().Be("/Api/User/SampleIdentityRegister");
        endpoint.User.Identity.LoginController.Should().BeOfType<UserIdentityLoginFor>().Which.ControllerRoute.Should().Be("/Api/User/SampleIdentityLogin");
        endpoint.User.Identity.PasswordController.Should().BeOfType<SampleIdentityPasswordFor>().Which.ControllerRoute.Should().Be("/Api/User/SampleIdentityPassword");
        endpoint.User.Identity.ManagementController.Should().BeOfType<SampleIdentityManagementFor>().Which.ControllerRoute.Should().Be("/Api/User/SampleIdentityManagement");
        endpoint.User.PP.Should().BeOfType<ProfilePictureFor>().Which.ControllerRoute.Should().Be("/Api/User/ProfilePicture");
        endpoint.Sample.ClientError.ControllerRoute.Should().BeEmpty();
        endpoint.Sample.NexusStatus.ControllerRoute.Should().BeEmpty();
    }
}
