using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Utils.Auth;
using DRN.Test.Tests.Sample.Controller.Helpers;
using Sample.Hosted;
using Sample.Hosted.Controllers;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Sample.Controller;

public class PrivateControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task Authorized_Action_Should_Return_AuthenticatedUser(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        var testUser = await AuthenticationHelper.AuthenticateClientAsync(client);

        var userSummary = await client.GetFromJsonAsync<ScopedUserSummary>(ApiFor.Sample.Private.Authorized.RoutePattern);
        userSummary.Should().NotBeNull();
        userSummary?.Authenticated.Should().BeTrue();
    }

    [Theory]
    [DataInline]
    public async Task Anonymous_Action_Should_Return_AnonymousUser(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);

        var userSummary = await client.GetFromJsonAsync<ScopedUserSummary>(ApiFor.Sample.Private.Anonymous.RoutePattern);
        userSummary.Should().NotBeNull();
        userSummary?.Authenticated.Should().BeFalse();
    }

    [Theory]
    [DataInline]
    public async Task Validate_Scope_Action_Should_Request_ScopeContext(TestContext context, string username, string password)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        await AuthenticationHelper.AuthenticateClientAsync(client, username, password);

        var scopeContext = await client.GetAsync(ApiFor.Sample.Private.Context.RoutePattern);
        scopeContext.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Validate_Scope_Action_Should_Validate_Scope(TestContext context, string username, string password)
    {
        var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
        await AuthenticationHelper.AuthenticateClientAsync(client, username, password);

        var validation = await client.GetAsync(ApiFor.Sample.Private.ValidateScope.RoutePattern);
        validation.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}