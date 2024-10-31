using Sample.Hosted;
using Sample.Hosted.Controllers;

namespace DRN.Test.Tests.Sample.Controller;

public class EndpointForTests
{
    [Theory]
    [DataInline]
    public async Task EndPointFor_Should_Return_Endpoint_Address(TestContext context)
    {
        await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<Program>();

        var confirmEmailEndpoint = EndpointFor.User.Identity.Confirmation.ConfirmEmail;
        confirmEmailEndpoint.RoutePattern.Should().NotBeNull();

        var confirmationEndpoints = EndpointFor.User.Identity.Confirmation.Endpoints;
        confirmationEndpoints.Should().HaveCountGreaterThan(0);

        //todo: generate links compare named endpoints against controller action name
        //add default endpoint names
    }
}