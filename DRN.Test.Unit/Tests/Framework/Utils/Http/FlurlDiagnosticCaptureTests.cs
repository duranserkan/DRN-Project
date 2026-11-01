using System.Text;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using Flurl.Http;

namespace DRN.Test.Unit.Tests.Framework.Utils.Http;

public class FlurlDiagnosticCaptureTests
{
    [Fact]
    public async Task Error_Response_Capture_Should_Stop_At_The_Limit_Without_Disposing_The_Response()
    {
        using var settings = SettingsProvider.Development();
        var policy = new DiagnosticSanitizer(new DiagnosticLogOptions { MaxFieldBytes = 1024, MaxProcessingMilliseconds = 100 });
        var log = new ScopedLog(settings, policy);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/?token=request-probe");
        using var stream = new TrackingStream(new byte[10000]);
        using var message = new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway) { Content = new StreamContent(stream) };
        message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        var response = Substitute.For<IFlurlResponse>();
        response.ResponseMessage.Returns(message);
        var exception = CreateException(request, response);

        await exception.PrepareScopeLogForFlurlExceptionAsync(log, settings.Features, policy, CancellationToken.None);

        stream.BytesRead.Should().Be(1025);
        stream.Disposed.Should().BeFalse();
        response.DidNotReceive().Dispose();
        log.GetLogs()["FlurlEx_ResponseCaptureLimited"].Should().Be(true);
        log.GetLogs()["FlurlExCall_Duration_Seconds"].Should().Be(1.25d);
        log.ToString().Should().NotContain("request-probe");
        exception.Message.Should().Be("Upstream failed");
    }

    [Theory]
    [DataInlineUnit("utf-8", false, false, "probe")]
    [DataInlineUnit("utf-16", false, false, "[unsupported diagnostic encoding]")]
    [DataInlineUnit("utf-8", true, false, "[diagnostic response read unavailable]")]
    [DataInlineUnit("utf-8", false, true, "[diagnostic response read unavailable]")]
    public async Task Error_Response_Capture_Should_Restore_Seekable_Streams_On_Success_And_Failure(
        string charset, bool malformedUtf8, bool failRead, string expected)
    {
        using var settings = SettingsProvider.Development();
        var policy = new DiagnosticSanitizer(new DiagnosticLogOptions { MaxProcessingMilliseconds = 100 });
        var log = new ScopedLog(settings, policy);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        using var stream = new TrackingStream(malformedUtf8 ? [0, 0xff] : Encoding.UTF8.GetBytes("xprobe"),
            seekable: true, failRead: failRead);
        stream.Position = 1;
        using var message = new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway) { Content = new StreamContent(stream) };
        message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") { CharSet = charset };
        var response = Substitute.For<IFlurlResponse>();
        response.ResponseMessage.Returns(message);
        var exception = CreateException(request, response);

        await exception.PrepareScopeLogForFlurlExceptionAsync(log, settings.Features, policy, CancellationToken.None);

        stream.Position.Should().Be(1);
        stream.Disposed.Should().BeFalse();
        response.DidNotReceive().Dispose();
        log.GetLogs()["FlurlEx_Response"].Should().Be(expected);
        log.GetLogs().Should().NotContainKey("FlurlEx_ResponseCaptureLimited");
    }

    private static FlurlHttpException CreateException(HttpRequestMessage request, IFlurlResponse response)
    {
        var started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new FlurlHttpException(new FlurlCall
        {
            HttpRequestMessage = request, Response = response,
            StartedUtc = started, EndedUtc = started.AddMilliseconds(1250)
        }, "Upstream failed", null);
    }

    private sealed class TrackingStream(byte[] data, bool seekable = false, bool failRead = false) : MemoryStream(data)
    {
        public int BytesRead { get; private set; }
        public bool Disposed { get; private set; }
        public override bool CanSeek => seekable;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (failRead)
            {
                Position++;
                throw new IOException("Response stream failed after advancing.");
            }
            var count = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += count;
            return count;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
