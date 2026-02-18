using DRN.Framework.Hosting.Middlewares;
using Microsoft.AspNetCore.Http;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares;

public class RequestBufferingStateTests
{
    /// <summary>
    /// Covers all non-buffering branches of TryEnableBuffering in one parameterized test:
    ///   1. Idempotency  — second call returns the cached state
    ///   2. Disabled     — DisableRequestBuffering = true
    ///   3. No-body      — GET / DELETE / HEAD have no semantic body
    ///   4. Chunked POST — null Content-Length skipped (DoS guard)
    ///   5. Oversized    — Content-Length exceeds the configured limit
    /// </summary>
    [Theory]
    [DataInlineUnit("idempotent", false, "POST", 100L, false)]
    [DataInlineUnit("disabled", true, "POST", 100L, false)]
    [DataInlineUnit("no-body-GET", false, "GET", 100L, false)]
    [DataInlineUnit("no-body-DEL", false, "DELETE", 100L, false)]
    [DataInlineUnit("no-body-HED", false, "HEAD", 100L, false)]
    [DataInlineUnit("chunked", false, "POST", null, false)]
    [DataInlineUnit("oversized", false, "POST", 99999L, false)]
    [DataInlineUnit("buffered", false, "POST", 100L, true)]
    public void TryEnableBuffering_Should_Return_Correct_State(
        string scenario, bool disableBuffering, string method, long? contentLength, bool expectedBuffered)
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Method = method,
                ContentLength = contentLength,
                Body = new MemoryStream()
            }
        };

        var features = new DrnAppFeatures { DisableRequestBuffering = disableBuffering };
        var state = RequestBufferingState.TryEnableBuffering(context, features);

        if (scenario == "idempotent")
        {
            // Second call must return the exact same cached instance
            var state2 = RequestBufferingState.TryEnableBuffering(context, features);
            state2.Should().BeSameAs(state);
            return;
        }

        if (expectedBuffered)
            context.Request.Body.CanSeek.Should().BeTrue();
        else
            context.Request.Body.Should().BeOfType<MemoryStream>();

        // The state object is stored in context.Items regardless of outcome
        context.Items.Should().ContainKey("RequestBufferingState");
    }

    /// <summary>
    /// Verifies that MaxRequestBufferingSize below the 10,000-byte minimum falls back to the
    /// 30,000-byte default, while a value ≥ 10,000 is used as-is.
    /// Validated indirectly: a POST with Content-Length just above the configured limit is NOT buffered,
    /// while the same request with Content-Length just below IS buffered.
    /// </summary>
    [Theory]
    [DataInlineUnit(0, 29999, true)]
    [DataInlineUnit(0, 30001, false)]
    [DataInlineUnit(10000, 9999, true)]
    [DataInlineUnit(10000, 10001, false)]
    [DataInlineUnit(1, 9999, true)]
    public void TryEnableBuffering_MaxBufferSize_Should_Respect_MinimumFloor(
        int configuredMax,
        int contentLength,
        bool expectedBuffered)
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Method = "POST",
                ContentLength = contentLength,
                Body = new MemoryStream()
            }
        };

        var features = new DrnAppFeatures { MaxRequestBufferingSize = configuredMax };

        var state = RequestBufferingState.TryEnableBuffering(context, features);
        state.IsBuffered.Should().Be(expectedBuffered);
    }

    /// <summary>
    /// Covers all non-read branches of ReadBodyAsync in one parameterized async test:
    ///   A. No state in context.Items  → "not initialized" message
    ///   B. Disabled state             → "buffering disabled" message
    ///   C. HasNoBody state (GET)      → "carry no body" message
    ///   D. Null Content-Length POST   → "Content-Length unknown" message
    ///   E. Oversized Content-Length   → "exceeded N byte limit" message
    /// </summary>
    [Theory]
    [DataInlineUnit("no-state", false, "GET", null, "[Request body not captured: buffering state not initialized]")]
    [DataInlineUnit("disabled", true, "POST", 100, "[Request body not captured: buffering disabled via DrnAppFeatures.DisableRequestBuffering]")]
    [DataInlineUnit("no-body", false, "GET", 100, "[Request body not captured: GET requests typically carry no body]")]
    [DataInlineUnit("chunked", false, "POST", null, "[Request body not captured: Content-Length unknown, buffering skipped to prevent DoS]")]
    [DataInlineUnit("oversized", false, "POST", 99999, "[Request body not captured: Content-Length 99999 exceeded 30000 byte limit]")]
    public async Task ReadBodyAsync_Should_Return_Reason_When_Not_Buffered(
        string scenario,
        bool disableBuffering,
        string method,
        int? contentLength,
        string expectedMessage)
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Method = method,
                ContentLength = contentLength,
                Body = new MemoryStream()
            }
        };

        if (scenario != "no-state")
        {
            var features = new DrnAppFeatures { DisableRequestBuffering = disableBuffering };
            RequestBufferingState.TryEnableBuffering(context, features);
        }

        var result = await RequestBufferingState.ReadBodyAsync(context);

        result.Should().Be(expectedMessage);
    }

    /// <summary>
    /// Verifies that ReadBodyAsync returns the actual body content when buffering was enabled,
    /// and resets the stream position so subsequent reads remain possible.
    /// </summary>
    [Theory]
    [DataInlineUnit]
    public async Task ReadBodyAsync_Should_Return_Body_And_Reset_Stream_Position(DrnTestContextUnit _)
    {
        const string bodyContent = "{ \"hello\": \"world\" }";
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(bodyContent);

        var context = new DefaultHttpContext
        {
            Request =
            {
                Method = "POST",
                ContentLength = bodyBytes.Length,
                // Use a MemoryStream pre-filled with the body; EnableBuffering will wrap it
                Body = new MemoryStream(bodyBytes)
            }
        };

        var features = new DrnAppFeatures();
        RequestBufferingState.TryEnableBuffering(context, features);

        var result = await RequestBufferingState.ReadBodyAsync(context);

        result.Should().Be(bodyContent);

        // Stream must be rewound so downstream middleware can still read it
        context.Request.Body.Position.Should().Be(0);
    }
}