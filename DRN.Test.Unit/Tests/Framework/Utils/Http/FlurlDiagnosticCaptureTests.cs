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
        var started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var exception = new FlurlHttpException(new FlurlCall
        {
            HttpRequestMessage = request, Response = response,
            StartedUtc = started, EndedUtc = started.AddMilliseconds(1250)
        }, "Upstream failed", null);

        await exception.PrepareScopeLogForFlurlExceptionAsync(log, settings.Features, policy, CancellationToken.None);

        stream.BytesRead.Should().Be(1025);
        stream.Disposed.Should().BeFalse();
        response.DidNotReceive().Dispose();
        log.GetLogs()["FlurlEx_ResponseCaptureLimited"].Should().Be(true);
        log.GetLogs()["FlurlExCall_Duration_Seconds"].Should().Be(1.25d);
        log.ToString().Should().NotContain("request-probe");
        exception.Message.Should().Be("Upstream failed");
    }

    private sealed class TrackingStream(byte[] data) : MemoryStream(data)
    {
        public int BytesRead { get; private set; }
        public bool Disposed { get; private set; }
        public override bool CanSeek => false;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += count;
            return count;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
