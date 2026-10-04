using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using Flurl.Http;

namespace DRN.Test.Unit.Tests.Framework.Utils.Http;

public class LinkerdDiagnosticsTests
{
    [Fact]
    public async Task Diagnostics_Should_Capture_Only_Proxy_Headers_Without_Consuming_Response()
    {
        using var message = new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream unavailable")
        };
        message.Headers.Add("L5D-Proxy-Error", "connection refused");
        message.Headers.Add("L5D-Proxy-Connection", "close");
        message.Headers.Add("Set-Cookie", "session=private");
        var response = Substitute.For<IFlurlResponse>();
        response.ResponseMessage.Returns(message);
        var log = Substitute.For<IScopedLog>();

        response.AddLinkerdProxyDiagnostics(log);

        log.Received(1).AddIfNotNullOrEmpty("Response_l5d-proxy-error", "connection refused");
        log.Received(1).AddIfNotNullOrEmpty("Response_l5d-proxy-connection", "close");
        log.ReceivedCalls().Should().HaveCount(2);
        await response.DidNotReceive().GetStringAsync();
        response.DidNotReceive().Dispose();
        (await message.Content.ReadAsStringAsync()).Should().Be("upstream unavailable");
        message.Headers.GetValues("L5D-Proxy-Error").Should().ContainSingle().Which.Should().Be("connection refused");
    }

    [Fact]
    public void Diagnostics_Should_Not_Infer_Proxy_Errors_From_Status_Alone()
    {
        using var message = new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        var response = Substitute.For<IFlurlResponse>();
        response.ResponseMessage.Returns(message);
        var log = Substitute.For<IScopedLog>();

        response.AddLinkerdProxyDiagnostics(log);

        log.ReceivedCalls().Should().BeEmpty();
        response.DidNotReceive().Dispose();
    }
}
