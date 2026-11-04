using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using Flurl.Http;

namespace DRN.Test.Unit.Tests.Framework.Utils.Http;

public class LinkerdDiagnosticsTests
{
    private static readonly string[] HeaderValues = ["first", "second", "third"];

    [Theory]
    [DataInlineUnit("l5d-proxy-error", 1, "first")]
    [DataInlineUnit("l5d-proxy-error", 2, "first, second")]
    [DataInlineUnit("l5d-proxy-error", 3, DiagnosticSanitizer.Limited)]
    [DataInlineUnit("l5d-proxy-connection", 1, "first")]
    [DataInlineUnit("l5d-proxy-connection", 2, "first, second")]
    [DataInlineUnit("l5d-proxy-connection", 3, DiagnosticSanitizer.Limited)]
    public void Diagnostics_Should_Mark_Header_Collections_Only_When_Values_Exceed_The_Limit(
        string header, int count, string expected)
    {
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxCollectionEntries = 2, MaxProcessingMilliseconds = 100
        }));
        using var message = new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway);
        message.Headers.Add(header, HeaderValues.Take(count));
        var response = Substitute.For<IFlurlResponse>();
        response.ResponseMessage.Returns(message);

        response.AddLinkerdProxyDiagnostics(log);

        // Inspect capture independently of the connection header's name-based redaction.
        log.GetLogs(redact: false)["Response_" + header].Should().Be(expected);
        message.Headers.GetValues(header).Should().HaveCount(count);
        response.DidNotReceive().Dispose();
    }

    [Theory]
    [DataInlineUnit(DiagnosticSanitizer.Invalid)]
    [DataInlineUnit(DiagnosticSanitizer.Limited)]
    public void Diagnostics_Should_Preserve_Existing_Header_Markers(string marker)
    {
        using var message = new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway);
        message.Headers.Add("l5d-proxy-error", new[] { "first", marker });
        var response = Substitute.For<IFlurlResponse>();
        response.ResponseMessage.Returns(message);
        var log = Substitute.For<IScopedLog>();

        response.AddLinkerdProxyDiagnostics(log);

        log.Received(1).AddIfNotNullOrEmpty("Response_l5d-proxy-error", "first, " + marker);
    }

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
