using System.Runtime.CompilerServices;
using System.Text;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using DRN.Framework.Utils.Settings;
using Flurl.Http;
using Flurl.Http.Testing;

namespace DRN.Framework.Utils.Extensions;

public static class FlurlExtensions
{
    private const string ResponseKey = "FlurlEx_Response";

    public static int GetGatewayStatusCode(this FlurlHttpException exception) =>
        exception.StatusCode switch
        {
            >= 400 and < 500 => exception.StatusCode ?? 0,
            503 => 503,
            504 => 504,
            _ => 502
        };

    public static Task PrepareScopeLogForFlurlExceptionAsync(this FlurlHttpException ex, IScopedLog scopedLog, DrnAppFeatures appFeatures)
        => PrepareScopeLogForFlurlExceptionAsync(ex, scopedLog, appFeatures,
            GetDiagnosticSanitizer(scopedLog), CancellationToken.None);

    public static async Task PrepareScopeLogForFlurlExceptionAsync(this FlurlHttpException ex, IScopedLog scopedLog,
        DrnAppFeatures appFeatures, IDiagnosticSanitizer sanitizer, CancellationToken cancellationToken)
    {
        var call = ex.Call;
        var request = call.HttpRequestMessage;

        scopedLog.Add("FlurlEx_Method", request.Method.ToString());
        scopedLog.Add("FlurlEx_Version", request.Version.ToString());
        scopedLog.AddIfNotNullOrEmpty("FlurlEx_RequestUri", sanitizer.SanitizeUrl(request.RequestUri?.ToString()));
        scopedLog.AddIfNotNullOrEmpty("FlurlEx_VersionPolicy", request.VersionPolicy.ToString());

        scopedLog.Add("FlurlExCall_Completed", call.Completed);
        scopedLog.Add("FlurlExCall_Started", call.StartedUtc);
        if (call.EndedUtc != null)
            scopedLog.Add("FlurlExCall_Ended", call.EndedUtc);
        if (call.Duration != null)
            scopedLog.Add("FlurlExCall_Duration", call.Duration);

        if (call.Response == null) return;

        call.Response.AddLinkerdProxyDiagnostics(scopedLog);
        await CaptureResponseAsync(call.Response.ResponseMessage.Content, request.RequestUri, scopedLog, sanitizer, cancellationToken);
    }

    private static async Task CaptureResponseAsync(HttpContent content, Uri? requestUri, IScopedLog scopedLog,
        IDiagnosticSanitizer sanitizer, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await content.ReadAsStreamAsync(cancellationToken);
            var position = stream.CanSeek ? stream.Position : (long?)null;
            var buffer = new byte[sanitizer.Options.MaxFieldBytes + 1];
            try
            {
                var count = 0;
                while (count < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken);
                    if (read == 0) break;
                    count += read;
                }

                if (count > sanitizer.Options.MaxFieldBytes)
                {
                    scopedLog.Add(ResponseKey, DiagnosticSanitizer.Limited);
                    scopedLog.Add("FlurlEx_ResponseCaptureLimited", true);
                    return;
                }

                var charset = content.Headers.ContentType?.CharSet?.Trim('"');
                if (!string.IsNullOrEmpty(charset) && !charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
                {
                    scopedLog.Add(ResponseKey, "[unsupported diagnostic encoding]");
                    return;
                }

                var body = new UTF8Encoding(false, true).GetString(buffer, 0, count);
                scopedLog.Add(ResponseKey, sanitizer.SanitizeBody(body,
                    content.Headers.ContentType?.MediaType, requestUri?.AbsolutePath ?? string.Empty));
            }
            finally
            {
                Array.Clear(buffer);
                if (position.HasValue) stream.Position = position.Value;
            }
        }
        catch (Exception)
        {
            scopedLog.Add(ResponseKey, "[diagnostic response read unavailable]");
        }
    }

    private static IDiagnosticSanitizer GetDiagnosticSanitizer(IScopedLog scopedLog) =>
        (scopedLog as ScopedLog)?.Sanitizer ?? new DiagnosticSanitizer(new DiagnosticLogOptions());

    /// <summary>
    /// Records Linkerd proxy response headers without reading or disposing the response.
    /// Values are untrusted diagnostic data and must not be exposed to clients or used for authorization.
    /// </summary>
    public static void AddLinkerdProxyDiagnostics(this IFlurlResponse response, IScopedLog scopedLog)
    {
        var sanitizer = GetDiagnosticSanitizer(scopedLog);
        var headers = response.ResponseMessage.Headers;
        CaptureHeader("l5d-proxy-error");
        CaptureHeader("l5d-proxy-connection");

        void CaptureHeader(string name)
        {
            if (headers.TryGetValues(name, out var values))
                scopedLog.AddIfNotNullOrEmpty("Response_" + name, SafeHeader(values));
        }

        string SafeHeader(IEnumerable<string> values)
        {
            var result = new StringBuilder();
            var count = 0;
            foreach (var value in values)
            {
                if (count++ >= sanitizer.Options.MaxCollectionEntries) return DiagnosticSanitizer.Limited;
                var safe = sanitizer.SanitizeText(value);
                if (result.Length + safe.Length > sanitizer.Options.MaxFieldBytes) return DiagnosticSanitizer.Limited;
                if (result.Length > 0) result.Append(", ");
                result.Append(safe);
            }
            return result.ToString();
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_filteredSetups")]
    private static extern ref List<FilteredHttpTestSetup> GetFilteredSetups(HttpTest httpTest);

    public static HttpTest ClearFilteredSetups(this HttpTest httpTest)
    {
        GetFilteredSetups(httpTest).Clear();
        return httpTest;
    }
}
