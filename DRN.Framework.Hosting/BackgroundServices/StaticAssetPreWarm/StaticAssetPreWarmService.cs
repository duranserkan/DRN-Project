using System.Collections.Concurrent;
using System.Diagnostics;
using DRN.Framework.Hosting.Utils;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.Hosting.Utils.Vite.Models;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.BackgroundServices.StaticAssetPreWarm;

[HostedService]
public class StaticAssetPreWarmService(
    IViteManifest viteManifest,
    IAppStartupStatus startupStatus,
    IServerAddressResolver server,
    IServiceProvider scopeFactory) : BackgroundService
{
    private IScopedLog _scopedLog = null!;
    private ILogger _logger = null!;
    
    /// <summary>
    /// Accept-Encoding values to pre-warm. ResponseCaching keys on Vary: Accept-Encoding,
    /// so each distinct value populates a separate cache entry. Order: most-preferred first.
    /// </summary>
    private static readonly string[] AcceptEncodings = ["br", "gzip"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await startupStatus.WaitForStartAsync(stoppingToken))
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

    private async Task PreWarmAsync(CancellationToken stoppingToken)
    {
        var context = GetPreWarmContext(_scopedLog);
        if (context == null)
        {
            _logger.LogScoped(_scopedLog);
            return;
        }

        _scopedLog
            .Add(PreWarmScopeLogKeys.AssetCount, context.Items.Count)
            .Add(PreWarmScopeLogKeys.Encodings, AcceptEncodings.Length)
            .Add(PreWarmScopeLogKeys.BaseAddress, context.BaseAddress);

        var sw = Stopwatch.StartNew();
        var workItems = BuildWorkItems(context.Items);

        using var proxy = new StaticAssetPreWarmProxy(context.BaseAddress, _scopedLog);
        var assetReports = await proxy.ExecutePreWarmRequestsAsync(workItems, stoppingToken);
        sw.Stop();

        PublishReport(workItems.Count, assetReports, sw.ElapsedMilliseconds);
    }
    
    private PreWarmContext? GetPreWarmContext(IScopedLog scopedLog)
    {
        var items = viteManifest.GetAllManifestItems();
        if (items.Count == 0)
        {
            scopedLog.Add(PreWarmScopeLogKeys.SkipReason, "NoViteManifestItemsFound");
            return null;
        }

        var baseAddress = server.GetLoopbackAddress();
        if (baseAddress != null)
            return new PreWarmContext(items, baseAddress);

        scopedLog.Add(PreWarmScopeLogKeys.SkipReason, "NoServerAddressAvailable");
        return null;
    }

    private static List<PreWarmWorkItem> BuildWorkItems(IReadOnlyCollection<ViteManifestItem> items)
        => items.SelectMany(item => AcceptEncodings.Select(enc => new PreWarmWorkItem(item, enc))).ToList();

    private void PublishReport(int totalRequests, ConcurrentBag<ViteManifestPreWarmAssetReport> assetReports, long elapsedMs)
    {
        var sortedReports = assetReports.OrderBy(r => r.Path).ThenBy(r => r.ContentEncoding).ToList();
        var preWarmed = sortedReports.Count(r => r.Success);
        var report = new ViteManifestPreWarmReport(totalRequests, preWarmed, elapsedMs, sortedReports);

        ((ViteManifest)viteManifest).PreWarmReport = report;

        _logger.LogScoped(_scopedLog);
    }
}