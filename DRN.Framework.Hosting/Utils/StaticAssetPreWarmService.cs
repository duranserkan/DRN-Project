using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.Utils;

/// <summary>
/// Pre-warms the ResponseCaching middleware by requesting all Vite manifest
/// static assets through the HTTP pipeline at application startup.
/// <para>
/// When <see cref="Microsoft.AspNetCore.ResponseCaching.ResponseCachingMiddleware"/> and
/// <see cref="Microsoft.AspNetCore.ResponseCompression.ResponseCompressionMiddleware"/> are placed
/// before static file serving, the first request compresses the asset (expensive at SmallestSize)
/// and subsequent requests serve from the in-memory cache. This service shifts that initial
/// compression cost to startup time, ensuring no client pays it.
/// </para>
/// <para>
/// Assets are fetched in parallel to minimize total warmup wall-clock time.
/// </para>
/// </summary>
public class StaticAssetPreWarmService(
    IHostApplicationLifetime lifetime,
    IServer server,
    ILogger<StaticAssetPreWarmService> logger) : BackgroundService
{
    /// <summary>
    /// Maximum concurrent pre-warm requests. Keeps self-request pressure bounded
    /// to avoid starving the Kestrel thread pool during startup.
    /// </summary>
    private const int MaxParallelism = 4;

    /// <summary>
    /// Accept-Encoding values to pre-warm. ResponseCaching keys on Vary: Accept-Encoding,
    /// so each distinct value populates a separate cache entry. Order: most-preferred first.
    /// </summary>
    private static readonly string[] AcceptEncodings = ["br", "gzip"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await WaitForApplicationStartAsync(stoppingToken))
            return;

        try
        {
            await PreWarmAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown — no action needed
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Static asset pre-warming encountered an error");
        }
    }

    /// <summary>
    /// Waits until the application is fully started and listening for requests.
    /// Returns <c>false</c> if cancelled before startup completes.
    /// </summary>
    private async Task<bool> WaitForApplicationStartAsync(CancellationToken stoppingToken)
    {
        var startedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lifetime.ApplicationStarted.Register(() => startedTcs.TrySetResult());
        stoppingToken.Register(() => startedTcs.TrySetCanceled(stoppingToken));

        try
        {
            await startedTcs.Task;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task PreWarmAsync(CancellationToken stoppingToken)
    {
        if (!TryClaimAndValidate(out var items, out var baseAddress))
            return;

        logger.LogDebug("Pre-warming {Count} static assets x {Encodings} encodings via {BaseAddress} (parallelism: {MaxParallelism})",
            items.Count, AcceptEncodings.Length, baseAddress, MaxParallelism);

        var sw = Stopwatch.StartNew();
        var workItems = BuildWorkItems(items);
        var assetReports = await ExecutePreWarmRequestsAsync(workItems, baseAddress, stoppingToken);
        sw.Stop();

        PublishReport(workItems.Count, assetReports, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Atomically claims pre-warm ownership and validates prerequisites.
    /// Returns <c>false</c> (with appropriate logging) when pre-warming should be skipped.
    /// </summary>
    private bool TryClaimAndValidate(out IReadOnlyCollection<ViteManifestItem> items, out string baseAddress)
    {
        items = [];
        baseAddress = string.Empty;

        // Non-blocking claim: only the first instance proceeds, others skip instantly.
        // This prevents redundant work when multiple integration test instances
        // spin up in the same process — no blocking, no wasted requests.
        if (!ViteManifest.TryClaimPreWarm())
        {
            LogPreWarmSkipped();
            return false;
        }

        items = ViteManifest.GetAllManifestItems();
        if (items.Count == 0)
        {
            logger.LogDebug("No Vite manifest items found for pre-warming");
            return false;
        }

        var resolved = GetLoopbackAddress();
        if (resolved == null)
        {
            logger.LogDebug("No server address available for static asset pre-warming");
            return false;
        }

        baseAddress = resolved;
        return true;
    }

    /// <summary>
    /// Builds the cross-product of manifest items × encodings.
    /// Each (asset, encoding) pair produces an independent cache entry.
    /// </summary>
    private static List<(ViteManifestItem Item, string Encoding)> BuildWorkItems(IReadOnlyCollection<ViteManifestItem> items)
        => items.SelectMany(item => AcceptEncodings.Select(enc => (item, enc))).ToList();

    /// <summary>
    /// Executes all pre-warm requests in parallel with bounded concurrency.
    /// Returns collected asset reports (unordered).
    /// </summary>
    private async Task<ConcurrentBag<ViteManifestPreWarmAssetReport>> ExecutePreWarmRequestsAsync(
        List<(ViteManifestItem Item, string Encoding)> workItems, string baseAddress, CancellationToken stoppingToken)
    {
        var assetReports = new ConcurrentBag<ViteManifestPreWarmAssetReport>();

        // Security: DangerousAcceptAnyServerCertificateValidator is scoped to this loopback-only
        // pre-warm handler and disposed after completion — never exposed to external requests.
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };

        await Parallel.ForEachAsync(workItems, new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxParallelism,
            CancellationToken = stoppingToken
        }, async (work, ct) =>
        {
            assetReports.Add(await PreWarmSingleAssetAsync(client, work.Item, work.Encoding, ct));
        });

        return assetReports;
    }

    /// <summary>
    /// Sends a single pre-warm request and returns the asset report.
    /// Always reads the full response body to ensure the cache is fully populated.
    /// </summary>
    private async Task<ViteManifestPreWarmAssetReport> PreWarmSingleAssetAsync(
        HttpClient client, ViteManifestItem item, string encoding, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, item.Path);
            request.Headers.TryAddWithoutValidation("Accept-Encoding", encoding);

            using var response = await client.SendAsync(request, ct);
            var statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                sw.Stop();
                logger.LogDebug("Pre-warm: {StatusCode} for {Path} ({Encoding})", statusCode, item.Path, encoding);
                return ViteManifestPreWarmAssetReport.Failed(item.Path, statusCode, sw.ElapsedMilliseconds);
            }

            // Always read the full body to ensure ResponseCaching captures the complete response
            var body = await response.Content.ReadAsByteArrayAsync(ct);
            sw.Stop();

            var compressedBytes = (long)body.Length;
            var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var originalBytes = GetOriginalFileSize(item.Path);

            // When no compression was applied, compressed and original sizes are identical
            if (string.IsNullOrEmpty(contentEncoding))
                compressedBytes = originalBytes;

            return ViteManifestPreWarmAssetReport.Ok(
                item.Path, statusCode, originalBytes, compressedBytes,
                contentEncoding, contentType, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            logger.LogDebug(ex, "Pre-warm request failed for {Path} ({Encoding})", item.Path, encoding);
            return ViteManifestPreWarmAssetReport.Errored(item.Path, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Builds and publishes the pre-warm report as a write-once singleton.
    /// </summary>
    private void PublishReport(int totalRequests, ConcurrentBag<ViteManifestPreWarmAssetReport> assetReports, long elapsedMs)
    {
        var sortedReports = assetReports.OrderBy(r => r.Path).ThenBy(r => r.ContentEncoding).ToList();
        var preWarmed = sortedReports.Count(r => r.Success);
        var report = new ViteManifestPreWarmReport(totalRequests, preWarmed, elapsedMs, sortedReports);

        if (ViteManifest.TrySetPreWarmReport(report))
            logger.LogInformation("{Report}", report);
        else
            logger.LogDebug("Pre-warm report was already set by another instance, discarding duplicate");
    }

    /// <summary>
    /// Logs why pre-warming was skipped (already claimed or already completed).
    /// Uses structured logging to avoid string interpolation when debug logging is disabled.
    /// </summary>
    private void LogPreWarmSkipped()
    {
        var existingReport = ViteManifest.PreWarmReport;
        if (existingReport != null)
            logger.LogDebug("Static assets already pre-warmed (report created at {CreatedAt}), skipping", existingReport.CreatedAt);
        else
            logger.LogDebug("Static asset pre-warming already claimed by another instance, skipping");
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

    /// <summary>
    /// Resolves a loopback address from the server's bound addresses.
    /// Converts wildcard hosts (0.0.0.0, [::], +, *) to localhost for self-requests.
    /// Prefers HTTP over HTTPS to avoid TLS overhead for internal pre-warming.
    /// </summary>
    private string? GetLoopbackAddress()
    {
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        if (addressFeature == null) return null;

        string? httpsAddress = null;
        foreach (var address in addressFeature.Addresses)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
                continue;

            var host = NormalizeHost(uri.Host);
            var normalized = $"{uri.Scheme}://{host}:{uri.Port}";

            // Prefer HTTP to avoid TLS handshake overhead for self-requests
            if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
                return normalized;

            httpsAddress ??= normalized;
        }

        return httpsAddress;
    }

    private static string NormalizeHost(string host) => host switch
    {
        "0.0.0.0" or "[::]" or "+" or "*" => "localhost",
        _ => host
    };
}
