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
            (scopedLog as ScopedLog)?.Sanitizer ?? new DiagnosticSanitizer(new DiagnosticLogOptions()), CancellationToken.None);

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

        if (call.Response != null)
        {
            call.Response.AddLinkerdProxyDiagnostics(scopedLog);
            var content = call.Response.ResponseMessage.Content;
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
                    var charset = content.Headers.ContentType?.CharSet?.Trim('"');
                    var supportedEncoding = string.IsNullOrEmpty(charset) || charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase);
                    scopedLog.Add("FlurlEx_Response", count > sanitizer.Options.MaxFieldBytes ? DiagnosticSanitizer.Limited :
                        supportedEncoding ? sanitizer.SanitizeBody(new UTF8Encoding(false, true).GetString(buffer, 0, count),
                            content.Headers.ContentType?.MediaType, request.RequestUri?.AbsolutePath ?? string.Empty) : "[unsupported diagnostic encoding]");
                    if (count > sanitizer.Options.MaxFieldBytes)
                        scopedLog.Add("FlurlEx_ResponseCaptureLimited", true);
                }
                finally
                {
                    Array.Clear(buffer);
                    if (position.HasValue) stream.Position = position.Value;
                }
            }
            catch (Exception)
            {
                scopedLog.Add("FlurlEx_Response", "[diagnostic response read unavailable]");
            }
        }
    }

    /// <summary>
    /// Records Linkerd proxy response headers without reading or disposing the response.
    /// Values are untrusted diagnostic data and must not be exposed to clients or used for authorization.
    /// </summary>
    public static void AddLinkerdProxyDiagnostics(this IFlurlResponse response, IScopedLog scopedLog)
    {
        var sanitizer = (scopedLog as ScopedLog)?.Sanitizer ?? new DiagnosticSanitizer(new DiagnosticLogOptions());
        var headers = response.ResponseMessage.Headers;
        if (headers.TryGetValues("l5d-proxy-error", out var errors))
            scopedLog.AddIfNotNullOrEmpty("Response_l5d-proxy-error", SafeHeader(errors));
        if (headers.TryGetValues("l5d-proxy-connection", out var connections))
            scopedLog.AddIfNotNullOrEmpty("Response_l5d-proxy-connection", SafeHeader(connections));

        string SafeHeader(IEnumerable<string> values)
        {
            var result = new StringBuilder();
            foreach (var value in values.Take(sanitizer.Options.MaxCollectionEntries))
            {
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
