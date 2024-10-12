using System.Net;
using Sample.Hosted;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Sample.Controller;

public class ExceptionControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task ExceptionController_Should_Return_DrnException_Status_Codes(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        var response = await client.GetAsync("Exception/ValidationException");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        response = await client.GetAsync("Exception/UnauthorizedException");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        response = await client.GetAsync("Exception/ForbiddenException");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        response = await client.GetAsync("Exception/NotFoundException");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        response = await client.GetAsync("Exception/ConflictException");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        response = await client.GetAsync("Exception/ExpiredException");
        response.StatusCode.Should().Be(HttpStatusCode.Gone);

        response = await client.GetAsync("Exception/ConfigurationException");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        response = await client.GetAsync("Exception/UnprocessableEntityException");
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        Func<Task> malicious = async () => await client.GetAsync("Exception/MaliciousRequestException");
        await malicious.Should().ThrowAsync<OperationCanceledException>("The application aborted the request.");
    }
}