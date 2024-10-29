using System.Net;
using Sample.Hosted;
using Sample.Hosted.Controllers;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Sample.Controller;

public class ExceptionControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task ExceptionController_Should_Return_DrnException_Status_Codes(TestContext context)
    {
        var exceptionEndpoints = ApiFor.Sample.Exception;
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        var response = await client.GetAsync(exceptionEndpoints.ValidationException.RoutePattern);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        response = await client.GetAsync(exceptionEndpoints.UnauthorizedException.RoutePattern);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        response = await client.GetAsync(exceptionEndpoints.ForbiddenException.RoutePattern);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        response = await client.GetAsync(exceptionEndpoints.NotFoundException.RoutePattern);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        response = await client.GetAsync(exceptionEndpoints.ConflictException.RoutePattern);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        response = await client.GetAsync(exceptionEndpoints.ExpiredException.RoutePattern);
        response.StatusCode.Should().Be(HttpStatusCode.Gone);

        response = await client.GetAsync(exceptionEndpoints.ConfigurationException.RoutePattern);
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        response = await client.GetAsync(exceptionEndpoints.UnprocessableEntityException.RoutePattern);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        Func<Task> malicious = async () => await client.GetAsync(exceptionEndpoints.MaliciousRequestException.RoutePattern);
        await malicious.Should().ThrowAsync<OperationCanceledException>("The application aborted the request.");
    }
}