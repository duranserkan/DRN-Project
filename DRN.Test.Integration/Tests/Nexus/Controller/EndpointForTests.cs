using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.Hosting.Nexus;
using DRN.Nexus.Hosted;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Get = DRN.Nexus.Hosted.Helpers.Get;

namespace DRN.Test.Integration.Tests.Nexus.Controller;

public class NexusEndpointForTests
{
    [Theory]
    [DataInline]
    public async Task Accessors_Should_Preserve_Shared_Client_Routes_And_Http_Methods(DrnTestContext context)
    {
        var application = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<NexusProgram>();
        var generator = application.Services.GetRequiredService<LinkGenerator>();
        var http = new DefaultHttpContext { RequestServices = application.Services };
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("nexus.example.test");
        http.Request.PathBase = "/gateway";

        AssertRoute(Get.Endpoint.Sample.Status.Status, NexusEndpoints.Status, "/Api/Status", "GET");
        AssertRoute(Get.Endpoint.User.Identity.RegisterController.Register,
            NexusEndpoints.Identity.Register, "/Api/User/NexusIdentityRegister/Register", "POST");
        AssertRoute(Get.Endpoint.User.Identity.RegisterController.ConfirmEmail,
            NexusEndpoints.Identity.ConfirmEmail, "/Api/User/NexusIdentityRegister/ConfirmEmail", "GET");
        AssertRoute(Get.Endpoint.User.Identity.LoginController.Login,
            NexusEndpoints.Identity.Login, "/Api/User/NexusIdentityLogin/Login", "POST");

        void AssertRoute(ApiEndpoint endpoint, string clientPath, string expectedPath, string method)
        {
            ("/" + clientPath).Should().Be(expectedPath);
            endpoint.RouteEndpoints.Should().NotBeEmpty();
            endpoint.RouteEndpoints.Should().OnlyContain(route =>
                route.RoutePattern.RawText!.TrimStart('/') == expectedPath.TrimStart('/'));
            endpoint.RouteEndpoints.SelectMany(route => route.Metadata.GetRequiredMetadata<IHttpMethodMetadata>().HttpMethods)
                .Distinct().Should().Equal(method);
            generator.GetUriByEndpoint(http, endpoint).Should().Be("https://nexus.example.test/gateway" + expectedPath);
        }
    }
}
