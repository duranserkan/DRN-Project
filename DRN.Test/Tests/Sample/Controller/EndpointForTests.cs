using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Tests.Sample.Controller;

public class SampleEndpointForTests
{
    [Theory]
    [DataInline]
    public async Task EndPointFor_Should_Return_Endpoint_Address(DrnTestContext context)
    {
        await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<SampleProgram>();

        var confirmEmailEndpoint = Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
        confirmEmailEndpoint.RoutePattern.Should().NotBeNull();

        var confirmationEndpoints = Get.Endpoint.User.Identity.RegisterController.Endpoints;
        confirmationEndpoints.Should().HaveCountGreaterThan(0);

        //todo: generate links compare named endpoints against controller action name
        //add default endpoint names
    }
}