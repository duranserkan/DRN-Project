using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Time;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.DrnProgram;

[HostedService]
public class DrnBackgroundService(IHostApplicationLifetime lifetime, ILogger<DrnBackgroundService> logger, IScopedLog log, DrnAppFeatures features) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MonitorSystemDateTimeEvents();

        var monitorShutdown = features.UseMonotonicDateTimeProvider;
        if (!monitorShutdown)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            var shutdownRequested = await ShutdownRequested(stoppingToken);
            if (!shutdownRequested)
                continue;

            lifetime.StopApplication();
            return;
        }
    }

    private void MonitorSystemDateTimeEvents()
    {
        //It turns out the stopwatch-based implementation is slower than DateTimeOffset.UtcNow and not preferred anymore.
        //However, it would be nice to collect OnDriftCorrected and OnActionFailed metrics.
        MonotonicSystemDateTime.RecurringAction.OnActionFailed += e =>
        {
            log.AddException(e);
            log.AddToActions($"{nameof(MonotonicSystemDateTime)}.CheckClockDriftAsync has failed.");
            logger.LogScoped(log);

            if (!features.UseMonotonicDateTimeProvider)
                return;

            lifetime.StopApplication();
        };

        MonotonicSystemDateTime.OnDriftCorrected += info =>
        {
            log.Add("SystemDateTime", info.SystemDateTime);
            log.Add("MonotonicSystemDateTime", info.MonotonicSystemDateTime);
            log.Add("DriftTotalMilliseconds", info.Drift.TotalMilliseconds);
            log.Add("MonotonicDriftDetected", info.Drift.TotalMilliseconds);
            logger.LogScoped(log);
        };
    }

    private async Task<bool> ShutdownRequested(CancellationToken stoppingToken)
    {
        if (MonotonicSystemDateTime.IsShutdownRequested)
        {
            log.Add(nameof(MonotonicSystemDateTime), MonotonicSystemDateTime.UtcNow);
            log.Add("SystemDateTime", DateTimeProvider.UtcNow);
            log.AddException(new ConflictException($"Shutdown is requested by {nameof(MonotonicSystemDateTime)}"));

            logger.LogScoped(log);

            return true;
        }

        await Task.Delay(1000, stoppingToken); //1 second
        return false;
    }

    public override void Dispose()
    {
        MonotonicSystemDateTime.Dispose();
        base.Dispose();
    }
}