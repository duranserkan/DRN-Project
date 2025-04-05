using System.Diagnostics;

namespace DRN.Framework.Utils.Common.Time;

/// <summary>
/// Monotonic/System hybrid clock that Immune to drastic system clock changes and monotonic clock drifts
/// </summary>
public static class MonotonicSystemDateTime
{
    private static DateTimeOffset _initialTime;
    private static volatile bool _isShutdownRequested;

    private static readonly Lock SyncLock = new(); // Synchronization primitive
    private static readonly Stopwatch Stopwatch;
    private static readonly int UpdatePeriod = 10000; // 10 seconds

    /// <summary>
    /// Application should poll this flag to verify consistency
    /// </summary>
    public static bool IsShutdownRequested => _isShutdownRequested;

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
            lock (SyncLock) // Atomic read of both values
                return _initialTime + Stopwatch.Elapsed;
        }
    }

    /// <summary>
    /// Checks for clock drift between the monotonic time and system time.
    /// - If the system clock has changed by more than 1 minute, sets IsShutdownRequested to true.
    /// - If the change is less than 1 minute, adjusts the monotonic time to sync with the system clock.
    /// </summary>
    private static async Task CheckClockDriftAsync()
    {
        //when positive it means monotonic clock is lagged behind the actual value
        //when negative it means system clock is lagged behind the actual value
        var drift = DateTimeOffset.UtcNow - UtcNow;
        if (drift > TimeSpan.FromMinutes(1) || TimeSpan.FromMinutes(-1) > drift)
        {
            _isShutdownRequested = true;
            return;
        }

        // when drift < TimeSpan.Zero is true wait to catch monotonic clock
        // the MonotonicTime only moves forward even if it is not strict in this implementation
        if (drift <= TimeSpan.Zero)
        {
            while (true)
            {
                await Task.Delay(drift * -1); //long waits are handled by significant drift protection by shutdown request
                drift = DateTimeOffset.UtcNow - UtcNow;
                if (drift <= TimeSpan.Zero) continue;

                break;
            }
        }

        lock (SyncLock) // Atomic write of both values
        {
            Stopwatch.Restart();
            _initialTime = DateTimeOffset.UtcNow;
        }
    }
}