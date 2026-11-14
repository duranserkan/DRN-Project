using DRN.Nexus.Hosted.Helpers.EndpointFor;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Endpoints;

public class NexusAccessorConventionTests
{
    [Fact]
    public void Nexus_Accessors_Should_Preserve_Paths_Types_And_Shared_Routes()
    {
        var endpoint = new NexusEndpointFor();

        endpoint.Sample.Should().BeOfType<SampleApiFor>();
        endpoint.User.Should().BeOfType<UserApiFor>();
        endpoint.User.Identity.Should().BeOfType<UserIdentityFor>();
        endpoint.Sample.Status.Should().BeOfType<StatusFor>().Which.ControllerRoute.Should().Be("/Api/Status");
        endpoint.Sample.WeatherForecast.Should().BeOfType<WeatherForecastFor>().Which.ControllerRoute.Should().Be("/Api/WeatherForecast");
        endpoint.User.Identity.RegisterController.Should().BeOfType<UserIdentityRegisterFor>().Which.ControllerRoute.Should().Be("/Api/User/NexusIdentityRegister");
        endpoint.User.Identity.LoginController.Should().BeOfType<UserIdentityLoginFor>().Which.ControllerRoute.Should().Be("/Api/User/NexusIdentityLogin");
        endpoint.User.Identity.PasswordController.Should().BeOfType<UserIdentityPasswordFor>().Which.ControllerRoute.Should().Be("/Api/User/NexusIdentityPassword");
        endpoint.User.Identity.ManagementController.Should().BeOfType<UserIdentityManagementFor>().Which.ControllerRoute.Should().Be("/Api/User/NexusIdentityManagement");

        SampleApiFor.Prefix.Should().Be("/Api");
        SampleApiFor.ControllerRouteTemplate.Should().Be("/Api/[controller]");
        UserApiFor.Prefix.Should().Be("/Api/User");
        UserApiFor.ControllerRouteTemplate.Should().Be("/Api/User/[controller]");
    }
}
