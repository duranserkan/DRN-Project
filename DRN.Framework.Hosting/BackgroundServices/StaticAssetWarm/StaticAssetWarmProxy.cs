using System.Collections.Concurrent;
using System.Diagnostics;
using DRN.Framework.Hosting.Utils.Vite.Models;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Hosting;

namespace DRN.Framework.Hosting.BackgroundServices.StaticAssetWarm;

public sealed class StaticAssetWarmProxy(string baseAddress,IScopedLog scopedLog,
    IStaticAssetWarmProxyClientFactory clientFactory,  IWebHostEnvironment environment)
    : IDisposable
{
    private readonly HttpClient _client = clientFactory.GetClient(baseAddress);

    private readonly string _contentRoot = string.IsNullOrEmpty(environment.WebRootPath)
        ? environment.ContentRootPath
        : environment.WebRootPath;

    public async Task<ConcurrentBag<ViteManifestWarmAssetReport>> ExecutePreWarmRequestsAsync(List<WarmWorkItem> workItems, CancellationToken stoppingToken)
    {
        var assetReports = new ConcurrentBag<ViteManifestWarmAssetReport>();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = stoppingToken
        };

        await Parallel.ForEachAsync(workItems, options, async (work, ct)
            => assetReports.Add(await PreWarmSingleAssetAsync(work, ct)));

        return assetReports;
    }

    private async Task<ViteManifestWarmAssetReport> PreWarmSingleAssetAsync(WarmWorkItem work, CancellationToken ct)
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
                scopedLog.AddToList(WarmScopeLogKeys.FailedRequests, new
                {
                    work.Item.Path,
                    work.Encoding,
                    StatusCode = statusCode
                });

                return ViteManifestWarmAssetReport.Failed(work.Item.Path, statusCode, sw.ElapsedMilliseconds);
            }

            // Always read the full body to ensure ResponseCaching captures the complete response
            var body = await response.Content.ReadAsByteArrayAsync(ct);
            sw.Stop();

            var compressedBytes = (long)body.Length;
            var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var originalBytes = GetOriginalFileSize(_contentRoot, work.Item.Path);

            // When no compression was applied, compressed and original sizes are identical
            if (string.IsNullOrEmpty(contentEncoding))
                compressedBytes = originalBytes;

            return ViteManifestWarmAssetReport.Ok(
                work.Item.Path, statusCode, originalBytes, compressedBytes,
                contentEncoding, contentType, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            scopedLog.AddToList(WarmScopeLogKeys.ErroredRequests, new
            {
                work.Item.Path,
                work.Encoding,
                Error = ex.Message
            });

            return ViteManifestWarmAssetReport.Errored(work.Item.Path, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Resolves the original (uncompressed) file size from disk.
    /// Path is relative to wwwroot (e.g. "/app/app_preload.abc123.js").
    /// </summary>
    private static long GetOriginalFileSize(string contentRoot, string requestPath)
    {
        var filePath = Path.Combine(contentRoot, requestPath.TrimStart('/'));
        try
        {
            return File.Exists(filePath)
                ? new FileInfo(filePath).Length
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose() => clientFactory.Dispose();
}