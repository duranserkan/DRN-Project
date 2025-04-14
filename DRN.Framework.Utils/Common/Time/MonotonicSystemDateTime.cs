using System.Diagnostics;

namespace DRN.Framework.Utils.Common.Time;

/// <summary>
/// Monotonic/System hybrid clock that is immune to drastic system clock changes and monotonic clock drifts
/// </summary>
public static class MonotonicSystemDateTime
{
    private static DateTimeOffset _initialTime;
    private static volatile bool _isShutdownRequested;

    private static readonly ReaderWriterLockSlim SyncLock = new(); // Synchronization for high-frequency reads and low frequency writes
    private static readonly Stopwatch Stopwatch;
    private static readonly int UpdatePeriod = 10000; // 10 seconds

    /// <summary>
    /// Application should poll this flag to verify consistency
    /// </summary>
    public static bool IsShutdownRequested => _isShutdownRequested;

    internal static event Action<DriftInfo>? OnDriftCorrected;
    public static RecurringAction RecurringAction { get; private set; }

    //todo: test gracefully shutdown

    static MonotonicSystemDateTime()
    {
        _initialTime = DateTimeOffset.UtcNow;
        Stopwatch = Stopwatch.StartNew();

        RecurringAction = new RecurringAction(CheckClockDriftAsync, UpdatePeriod);
    }

    /// <summary>
    /// Gets the current UTC time using a monotonic clock (Stopwatch).
    /// Immune to system clock changes (e.g., NTP adjustments).
    /// </summary>
    public static DateTimeOffset UtcNow
    {
        get
        {
            SyncLock.EnterReadLock();
            var date = _initialTime + Stopwatch.Elapsed; // Atomic read of both values
            SyncLock.ExitReadLock();

            return date;
        }
    }

    /// <summary>
    /// Checks for clock drift between the monotonic time and system time.
    /// - If the system clock has changed by more than 1 minute, sets IsShutdownRequested to true.
    /// - If the change is less than 1 minute, adjusts the monotonic time to sync with the system clock.
    /// </summary>
    private static async Task CheckClockDriftAsync()
    {
        //When positive it means monotonic clock is lagged behind the actual value
        //When negative it means system clock is lagged behind the actual value
        var systemTime = DateTimeOffset.UtcNow;
        var monotonicSystemTime = UtcNow;
        var drift = systemTime - monotonicSystemTime;

        //5 milliseconds grace interval
        if (drift > TimeSpan.FromMilliseconds(-5) && drift < TimeSpan.FromMilliseconds(5))
            return;

        //long waits are handled by significant drift protection by shutdown request
        if (drift > TimeSpan.FromMinutes(1) || TimeSpan.FromMinutes(-1) > drift)
        {
            _isShutdownRequested = true;
            return;
        }

        // The MonotonicTime only moves forward even if it is not strict in this implementation
        // When drift is negative and less than 1 minute, monotonic clock waits to catch system clock
        // When drift is positive, monotonic clock is behind the system clock and can catch system clock directly, 
        if (drift <= TimeSpan.Zero)
            await Task.Delay(drift * -1 + TimeSpan.FromMicroseconds(2));

        SyncLock.EnterWriteLock(); // Atomic write of both values
        Stopwatch.Restart();
        _initialTime = DateTimeOffset.UtcNow;
        SyncLock.ExitWriteLock();

        OnDriftCorrected?.Invoke(new DriftInfo(systemTime, monotonicSystemTime, drift));
    }

    /// <summary>
    /// Disposes the resources used by the MonotonicSystemDateTime.
    /// </summary>
    internal static void Dispose()
    {
        RecurringAction.Dispose();
        SyncLock.Dispose();
    }
}

public record DriftInfo(DateTimeOffset SystemDateTime, DateTimeOffset MonotonicSystemDateTime, TimeSpan Drift);