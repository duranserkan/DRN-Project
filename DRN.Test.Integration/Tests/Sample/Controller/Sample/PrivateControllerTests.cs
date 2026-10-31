using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Utils.Auth;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Controller.Sample;

public class PrivateControllerTests
{
    [Theory]
    [DataInline]
    public async Task Private_Actions_Should_Preserve_User_Identity_And_Request_Scope(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        var anonymous = await client.GetFromJsonAsync<ScopedUserSummary>(Get.Endpoint.Sample.Private.Anonymous.RoutePattern);
        anonymous.Should().NotBeNull();
        anonymous.Authenticated.Should().BeFalse();

        await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);

        var userSummary = await client.GetFromJsonAsync<ScopedUserSummary>(Get.Endpoint.Sample.Private.Authorized.RoutePattern);
        userSummary.Should().NotBeNull();
        userSummary.Authenticated.Should().BeTrue();
        using var scopeContext = await client.GetAsync(Get.Endpoint.Sample.Private.Context.RoutePattern);
        scopeContext.StatusCode.Should().Be(HttpStatusCode.OK);
        using var validation = await client.GetAsync(Get.Endpoint.Sample.Private.ValidateScope.RoutePattern);
        validation.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
