using System.Buffers;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares;

/// <summary>
/// Manages request body buffering with a size gate to prevent memory/disk exhaustion.
/// <list type="bullet">
///   <item><b>Producer:</b> <see cref="TryEnableBuffering"/> — called in <see cref="HttpScopeMiddleware"/></item>
///   <item><b>Consumer:</b> <see cref="ReadBodyAsync"/> — called in
///     <see cref="ExceptionHandler.Utils.ExceptionUtils.CreateErrorPageModelAsync"/></item>
/// </list>
/// Buffering is only enabled for methods that semantically carry a body (POST, PUT, PATCH).
/// Requests with Content-Length exceeding the configured max buffer size (default 30,000 bytes) are not buffered,
/// and <see cref="ReadBodyAsync"/> returns a descriptive reason for unbuffered requests.
/// </summary>
public class RequestBufferingState
{
    private const int MinBufferSize = 10000;
    private const int DefaultBufferSize = 30000;
    private const string Key = nameof(RequestBufferingState);
    public bool IsBuffered { get; init; }
    public bool IsDisabled { get; init; }
    public bool HasNoBody { get; init; }
    public int BufferSizeLimit { get; init; }

    /// <summary>
    /// Enables request body buffering for body-carrying methods (POST, PUT, PATCH)
    /// when Content-Length is known and within <paramref name="features"/> max buffer size.
    /// Bodyless methods and unknown Content-Length (e.g., chunked transfer) are skipped.
    /// Stores the state in <see cref="HttpContext.Items"/> for downstream consumers.
    /// </summary>
    public static RequestBufferingState TryEnableBuffering(HttpContext context, DrnAppFeatures features)
    {
        if (context.Items.TryGetValue(Key, out var existing) && existing is RequestBufferingState existingState)
            return existingState;

        var maxBufferSize = features.MaxRequestBufferingSize >= MinBufferSize
            ? features.MaxRequestBufferingSize
            : DefaultBufferSize;

        if (features.DisableRequestBuffering)
        {
            var state = new RequestBufferingState { IsBuffered = false, IsDisabled = true, BufferSizeLimit = maxBufferSize };
            context.Items[Key] = state;
            return state;
        }

        // OPTIMIZATION: Skip buffering for methods that semantically carry no body.
        // POST, PUT, PATCH are the only methods where body capture is meaningful.
        // GET, HEAD, DELETE, OPTIONS, TRACE either have no body or the body has no defined semantics (RFC 9110 §9).
        var method = context.Request.Method;
        var hasBody = HttpMethods.IsPost(method) || HttpMethods.IsPatch(method) || HttpMethods.IsPut(method);
        if (!hasBody)
        {
            var state = new RequestBufferingState { IsBuffered = false, HasNoBody = true, BufferSizeLimit = maxBufferSize };
            context.Items[Key] = state;
            return state;
        }

        // SECURITY: Only buffer when Content-Length is known and within limit.
        // - Null Content-Length: chunked POST/PUT/PATCH (rare in practice).
        //   Skipped to prevent unbounded buffering (DoS vector). Trade-off: no body capture for chunked requests.
        // - Falsified Content-Length: Kestrel enforces Content-Length differently per protocol:
        //   • HTTP/1.1: Tracks bytes read against declared Content-Length via _unexaminedInputLength countdown
        //     and slices read buffers (Buffer.Slice(0, maxLength)) to never exceed it. Extra bytes beyond the
        //     declared length are excluded from the current request body (kept on the connection for pipelining).
        //   • HTTP/2: Enforces strictly per RFC 7540 §8.1.2.6 — if DATA payload exceeds declared Content-Length,
        //     Kestrel throws Http2StreamErrorException with PROTOCOL_ERROR and resets the stream (RST_STREAM).
        //     Fewer bytes than declared also triggers a PROTOCOL_ERROR on END_STREAM.
        //   • HTTP/3: No Content-Length enforcement at the body reader level; body framing is handled entirely
        //     by QUIC stream DATA frames and END_STREAM. MaxRequestBodySize is still enforced.
        var contentLength = context.Request.ContentLength;
        var shouldBuffer = contentLength is not null && contentLength <= maxBufferSize;
        if (shouldBuffer)
            context.Request.EnableBuffering(bufferLimit: maxBufferSize);

        var result = new RequestBufferingState { IsBuffered = shouldBuffer, BufferSizeLimit = maxBufferSize };
        context.Items[Key] = result;

        return result;
    }

    /// <summary>
    /// Safely reads the request body if buffering was enabled, up to <see cref="BufferSizeLimit"/> bytes.
    /// Returns a descriptive reason when the request was not buffered.
    /// Resets the stream position after reading so subsequent reads remain possible.
    /// </summary>
    public static async Task<string> ReadBodyAsync(HttpContext context)
    {
        if (!context.Items.TryGetValue(Key, out var obj) || obj is not RequestBufferingState state)
            return "[Request body not captured: buffering state not initialized]";

        if (!state.IsBuffered)
        {
            if (state.IsDisabled)
                return "[Request body not captured: buffering disabled via DrnAppFeatures.DisableRequestBuffering]";

            if (state.HasNoBody)
                return $"[Request body not captured: {context.Request.Method} requests typically carry no body]";

            var length = context.Request.ContentLength;
            return length is null
                ? "[Request body not captured: Content-Length unknown, buffering skipped to prevent DoS]"
                : $"[Request body not captured: Content-Length {length} exceeded {state.BufferSizeLimit} byte limit]";
        }

        var stream = context.Request.Body;
        if (!stream.CanSeek)
            return "[Request body not captured: stream is not seekable]";

        stream.Seek(0, SeekOrigin.Begin);

        var bufferSize = state.BufferSizeLimit;
        var buffer = ArrayPool<char>.Shared.Rent(bufferSize);
        try
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            var charsRead = await reader.ReadAsync(buffer.AsMemory(0, bufferSize));
            stream.Seek(0, SeekOrigin.Begin);
            return new string(buffer, 0, charsRead);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}