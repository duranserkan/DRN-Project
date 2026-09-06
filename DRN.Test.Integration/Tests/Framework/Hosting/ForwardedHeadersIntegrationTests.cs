using System.Net;
using System.Text.Json;
using DRN.Test.Utils.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DRN.Test.Integration.Tests.Framework.Hosting;

public class ForwardedHeadersIntegrationTests
{
    [Theory]
    [DataInline("10.0.0.10", true, false, true)]
    [DataInline("10.0.0.10", false, false, false)]
    [DataInline("203.0.113.10", true, false, false)]
    [DataInline("203.0.113.10", true, true, true)]
    public async Task ForwardedHeaders_Should_Apply_Only_From_Trusted_Proxies(
        DrnTestContext context, string proxyAddress, bool trustPrivateNetworks, bool explicitlyTrustProxy, bool expectedForwarding)
    {
        context.AddToConfiguration("AllowedHosts", "internal.example;public.example");
        context.AddToConfiguration("ForwardedHeaders:TrustPrivateNetworks", trustPrivateNetworks.ToString());
        if (explicitlyTrustProxy)
            context.AddToConfiguration("ForwardedHeaders:KnownProxies:0", proxyAddress);

        var application = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<ForwardedHeadersTestProgram>();
        application.Services.GetRequiredService<IConfiguration>().GetValue<bool>("ForwardedHeaders:TrustPrivateNetworks")
            .Should().Be(trustPrivateNetworks);
        var options = application.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        var peer = IPAddress.Parse(proxyAddress);
        (options.KnownProxies.Contains(peer) || options.KnownIPNetworks.Any(network => network.Contains(peer)))
            .Should().Be(expectedForwarding);

        var response = await application.Server.SendAsync(httpContext =>
        {
            // Set the actual transport peer; forwarding trust must not come from a request header.
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(proxyAddress);
            httpContext.Request.Method = HttpMethods.Get;
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("internal.example");
            httpContext.Request.Path = ForwardedHeadersTestProgram.ResourcePath;
            httpContext.Request.Headers["X-Forwarded-For"] = "198.51.100.25";
            httpContext.Request.Headers["X-Forwarded-Proto"] = "https";
            httpContext.Request.Headers["X-Forwarded-Host"] = "public.example";
            httpContext.Request.Headers["X-Forwarded-Prefix"] = "/gateway";
        });

        response.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        await using var body = response.Response.Body;
        var resource = await JsonSerializer.DeserializeAsync<ForwardedHeadersResource>(body, JsonSerializerOptions.Web);
        resource.Should().Be(new ForwardedHeadersResource(
            "test-resource",
            expectedForwarding ? "https" : "http",
            expectedForwarding,
            expectedForwarding ? "public.example" : "internal.example",
            expectedForwarding ? "/gateway" : string.Empty,
            ForwardedHeadersTestProgram.ResourcePath,
            expectedForwarding ? "198.51.100.25" : proxyAddress));
    }
}
