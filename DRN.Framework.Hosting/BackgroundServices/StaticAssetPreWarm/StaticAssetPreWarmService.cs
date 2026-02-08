using System.Collections.Concurrent;
using System.Diagnostics;
using DRN.Framework.Hosting.Utils;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.BackgroundServices.StaticAssetPreWarm;

public class StaticAssetPreWarmService(
    IHostApplicationLifetime lifetime,
    IServer server,
    IServiceProvider scopeFactory) : BackgroundService
{
    private IScopedLog _scopedLog = null!;
    private ILogger _logger = null!;
    
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

        using var scope = scopeFactory.CreateScope();
        _scopedLog = scope.ServiceProvider.GetRequiredService<IScopedLog>().WithLoggerName(nameof(StaticAssetPreWarmService));
        _logger = scope.ServiceProvider.GetRequiredService<ILogger<StaticAssetPreWarmService>>();

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
            _scopedLog.AddException(ex, "Static asset pre-warming encountered an error");
            _logger.LogScoped(_scopedLog);
        }
    }

    /// <summary>
    /// Waits until the application is fully started and listening for requests.
    /// Returns <c>false</c> if canceled before startup completes.
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
        var context = TryClaimAndValidate(_scopedLog);
        if (context == null)
        {
            _logger.LogScoped(_scopedLog);
            return;
        }

        _scopedLog
            .Add(PreWarmScopeLogKeys.AssetCount, context.Items.Count)
            .Add(PreWarmScopeLogKeys.Encodings, AcceptEncodings.Length)
            .Add(PreWarmScopeLogKeys.BaseAddress, context.BaseAddress)
            .Add(PreWarmScopeLogKeys.MaxParallelism, MaxParallelism);

        var sw = Stopwatch.StartNew();
        var workItems = BuildWorkItems(context.Items);

        using var proxy = new StaticAssetPreWarmProxy(context.BaseAddress, _scopedLog);
        var assetReports = await proxy.ExecutePreWarmRequestsAsync(workItems, MaxParallelism, stoppingToken);
        sw.Stop();

        PublishReport(workItems.Count, assetReports, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Atomically claims pre-warm ownership and validates prerequisites.
    /// Returns <c>null</c> (with appropriate logging) when pre-warming should be skipped.
    /// </summary>
    private PreWarmContext? TryClaimAndValidate(IScopedLog scopedLog)
    {
        // Non-blocking claim: only the first instance proceeds, others skip instantly.
        // This prevents redundant work when multiple integration test instances
        // spin up in the same process — no blocking, no wasted requests.
        if (!ViteManifest.TryClaimPreWarm())
        {
            var existingReport = ViteManifest.PreWarmReport;
            if (existingReport != null)
                scopedLog
                    .Add(PreWarmScopeLogKeys.SkipReason, "AlreadyPreWarmed")
                    .Add(PreWarmScopeLogKeys.ExistingReportCreatedAt, existingReport.CreatedAt);
            else
                scopedLog.Add(PreWarmScopeLogKeys.SkipReason, "AlreadyClaimedByAnotherInstance");
            return null;
        }

        var items = ViteManifest.GetAllManifestItems();
        if (items.Count == 0)
        {
            scopedLog.Add(PreWarmScopeLogKeys.SkipReason, "NoViteManifestItemsFound");
            return null;
        }

        var baseAddress = GetLoopbackAddress();
        if (baseAddress == null)
        {
            scopedLog.Add(PreWarmScopeLogKeys.SkipReason, "NoServerAddressAvailable");
            return null;
        }

        return new PreWarmContext(items, baseAddress);
    }

    /// <summary>
    /// Builds the cross-product of manifest items × encodings.
    /// Each (asset, encoding) pair produces an independent cache entry.
    /// </summary>
    private static List<PreWarmWorkItem> BuildWorkItems(IReadOnlyCollection<ViteManifestItem> items)
        => items.SelectMany(item => AcceptEncodings.Select(enc => new PreWarmWorkItem(item, enc))).ToList();

    /// <summary>
    /// Builds and publishes the pre-warm report as a write-once singleton.
    /// </summary>
    private void PublishReport(int totalRequests, ConcurrentBag<ViteManifestPreWarmAssetReport> assetReports,
        long elapsedMs)
    {
        var sortedReports = assetReports.OrderBy(r => r.Path).ThenBy(r => r.ContentEncoding).ToList();
        var preWarmed = sortedReports.Count(r => r.Success);
        var report = new ViteManifestPreWarmReport(totalRequests, preWarmed, elapsedMs, sortedReports);

        if (ViteManifest.TrySetPreWarmReport(report))
        {
            _scopedLog.Add(PreWarmScopeLogKeys.PreWarmReport, report);
            _scopedLog.AddToActions("PublishedPreWarmReport");
        }
        else
        {
            _scopedLog.Add(PreWarmScopeLogKeys.PreWarmReportStatus, "AlreadySetByAnotherInstance");
        }

        _logger.LogScoped(_scopedLog);
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