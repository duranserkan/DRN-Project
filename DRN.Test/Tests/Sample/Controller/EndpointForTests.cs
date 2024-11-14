using Sample.Hosted;
using Sample.Hosted.Controllers;

namespace DRN.Test.Tests.Sample.Controller;

public class SampleEndpointForTests
{
    [Theory]
    [DataInline]
    public async Task EndPointFor_Should_Return_Endpoint_Address(TestContext context)
    {
        await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<SampleProgram>();

        var confirmEmailEndpoint = SampleEndpointFor.User.Identity.RegisterController.ConfirmEmail;
        confirmEmailEndpoint.RoutePattern.Should().NotBeNull();

        var confirmationEndpoints = SampleEndpointFor.User.Identity.RegisterController.Endpoints;
        confirmationEndpoints.Should().HaveCountGreaterThan(0);

        //todo: generate links compare named endpoints against controller action name
        //add default endpoint names
    }
}