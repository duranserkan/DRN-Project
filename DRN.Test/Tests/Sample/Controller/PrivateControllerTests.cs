using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Utils.Auth;
using DRN.Test.Tests.Sample.Controller.Helpers;
using Sample.Hosted;
using Sample.Hosted.Helpers;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Sample.Controller;

public class PrivateControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task Authorized_Action_Should_Return_AuthenticatedUser(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        var user = await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);

        var userSummary = await client.GetFromJsonAsync<ScopedUserSummary>(Get.Endpoint.Sample.Private.Authorized.RoutePattern);
        userSummary.Should().NotBeNull();
        userSummary?.Authenticated.Should().BeTrue();
    }

    [Theory]
    [DataInline]
    public async Task Anonymous_Action_Should_Return_AnonymousUser(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);

        var userSummary = await client.GetFromJsonAsync<ScopedUserSummary>(Get.Endpoint.Sample.Private.Anonymous.RoutePattern);
        userSummary.Should().NotBeNull();
        userSummary?.Authenticated.Should().BeFalse();
    }

    [Theory]
    [DataInline]
    public async Task Validate_Scope_Action_Should_Request_ScopeContext(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);

        var scopeContext = await client.GetAsync(Get.Endpoint.Sample.Private.Context.RoutePattern);
        scopeContext.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Validate_Scope_Action_Should_Validate_Scope(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);

        var validation = await client.GetAsync(Get.Endpoint.Sample.Private.ValidateScope.RoutePattern);
        validation.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}