using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Utils.Auth;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Controller.Sample;

public class PrivateControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task Authorized_Action_Should_Return_AuthenticatedUser(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        var user = await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);

        var userSummary = await client.GetFromJsonAsync<ScopedUserSummary>(Get.Endpoint.Sample.Private.Authorized.RoutePattern);
        userSummary.Should().NotBeNull();
        userSummary?.Authenticated.Should().BeTrue();
    }

    [Theory]
    [DataInline]
    public async Task Anonymous_Action_Should_Return_AnonymousUser(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);

        var userSummary = await client.GetFromJsonAsync<ScopedUserSummary>(Get.Endpoint.Sample.Private.Anonymous.RoutePattern);
        userSummary.Should().NotBeNull();
        userSummary?.Authenticated.Should().BeFalse();
    }

    [Theory]
    [DataInline]
    public async Task Validate_Scope_Action_Should_Request_ScopeContext(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);

        var scopeContext = await client.GetAsync(Get.Endpoint.Sample.Private.Context.RoutePattern);
        scopeContext.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Validate_Scope_Action_Should_Validate_Scope(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);

        var validation = await client.GetAsync(Get.Endpoint.Sample.Private.ValidateScope.RoutePattern);
        validation.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}