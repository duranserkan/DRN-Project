using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Time;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.DrnProgram;

public class DrnBackgroundService(IHostApplicationLifetime lifetime, ILogger<DrnBackgroundService> logger, IScopedLog log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MonotonicSystemDateTime.RecurringAction.OnActionFailed += e =>
        {
            log.AddException(e);
            log.AddToActions($"{nameof(MonotonicSystemDateTime)}.CheckClockDriftAsync has failed.");
            logger.LogScoped(log);

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

        while (!stoppingToken.IsCancellationRequested)
        {
            if (MonotonicSystemDateTime.IsShutdownRequested)
            {
                log.Add(nameof(MonotonicSystemDateTime), MonotonicSystemDateTime.UtcNow);
                log.Add("SystemDateTime", DateTimeOffset.UtcNow);
                log.AddException(new ConflictException($"Shutdown is requested by {nameof(MonotonicSystemDateTime)}"));
                
                logger.LogScoped(log);
                lifetime.StopApplication();

                return;
            }

            await Task.Delay(1000, stoppingToken); //1 seconds
        }
    }
    
    public override void Dispose()
    {
        MonotonicSystemDateTime.Dispose();
        base.Dispose();
    }
}