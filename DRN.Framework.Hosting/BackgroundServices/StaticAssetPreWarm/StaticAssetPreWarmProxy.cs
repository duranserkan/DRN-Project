using System.Collections.Concurrent;
using System.Diagnostics;
using DRN.Framework.Hosting.Utils;
using DRN.Framework.Utils.Logging;

namespace DRN.Framework.Hosting.BackgroundServices.StaticAssetPreWarm;

public sealed class StaticAssetPreWarmProxy : IDisposable
{
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _client;
    private readonly IScopedLog _scopedLog;

    public StaticAssetPreWarmProxy(string baseAddress, IScopedLog scopedLog)
    {
        _scopedLog = scopedLog;

        // Security: DangerousAcceptAnyServerCertificateValidator is scoped to this loopback-only
        // pre-warm handler and disposed after completion — never exposed to external requests.
        _handler = new HttpClientHandler();
        _handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        _client = new HttpClient(_handler);
        _client.BaseAddress = new Uri(baseAddress);
    }

    public async Task<ConcurrentBag<ViteManifestPreWarmAssetReport>> ExecutePreWarmRequestsAsync(
        List<PreWarmWorkItem> workItems, int maxParallelism, CancellationToken stoppingToken)
    {
        var assetReports = new ConcurrentBag<ViteManifestPreWarmAssetReport>();

        await Parallel.ForEachAsync(workItems, new ParallelOptions
        {
            MaxDegreeOfParallelism = maxParallelism,
            CancellationToken = stoppingToken
        }, async (work, ct) => { assetReports.Add(await PreWarmSingleAssetAsync(work, ct)); });

        return assetReports;
    }

    private async Task<ViteManifestPreWarmAssetReport> PreWarmSingleAssetAsync(PreWarmWorkItem work, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, work.Item.Path);
            request.Headers.TryAddWithoutValidation("Accept-Encoding", work.Encoding);

            using var response = await _client.SendAsync(request, ct);
            var statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                sw.Stop();
                _scopedLog.AddToList(PreWarmScopeLogKeys.FailedRequests,
                    new { work.Item.Path, work.Encoding, StatusCode = statusCode });
                return ViteManifestPreWarmAssetReport.Failed(work.Item.Path, statusCode, sw.ElapsedMilliseconds);
            }

            // Always read the full body to ensure ResponseCaching captures the complete response
            var body = await response.Content.ReadAsByteArrayAsync(ct);
            sw.Stop();

            var compressedBytes = (long)body.Length;
            var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var originalBytes = GetOriginalFileSize(work.Item.Path);

            // When no compression was applied, compressed and original sizes are identical
            if (string.IsNullOrEmpty(contentEncoding))
                compressedBytes = originalBytes;

            return ViteManifestPreWarmAssetReport.Ok(
                work.Item.Path, statusCode, originalBytes, compressedBytes,
                contentEncoding, contentType, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _scopedLog.AddToList(PreWarmScopeLogKeys.ErroredRequests, new { work.Item.Path, work.Encoding, Error = ex.Message });
            return ViteManifestPreWarmAssetReport.Errored(work.Item.Path, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Resolves the original (uncompressed) file size from disk.
    /// Path is relative to wwwroot (e.g. "/app/app_preload.abc123.js").
    /// </summary>
    private static long GetOriginalFileSize(string requestPath)
    {
        try
        {
            var filePath = Path.Combine("wwwroot", requestPath.TrimStart('/'));
            return File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }
}