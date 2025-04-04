using System.Diagnostics;

namespace DRN.Framework.Utils.Common.Sequences;

/// <summary>
/// Monotonic/System hybrid clock that Immune to drastic system clock changes and monotonic clock drifts
/// </summary>
public static class MonotonicSystemHybridDateTime
{
    private static DateTimeOffset _initialTime;

    private static readonly Timer Timer;
    private static readonly Stopwatch Stopwatch;
    private static readonly int UpdatePeriod = 10000; // 10 seconds

    /// <summary>
    /// Application should poll this flag to verify consistency
    /// </summary>
    public static bool IsShutdownRequested { get; private set; }

    static MonotonicSystemHybridDateTime()
    {
        _initialTime = DateTimeOffset.UtcNow;
        Stopwatch = Stopwatch.StartNew();
        Timer = new Timer(_ => CheckClockDrift(), null, 0, UpdatePeriod); //todo Use a single-fire timer that reschedules itself to prevent overlapping callbacks.
    }

    /// <summary>
    /// Gets the current UTC time using a monotonic clock (Stopwatch).
    /// Immune to system clock changes (e.g., NTP adjustments).
    /// </summary>
    public static DateTimeOffset UtcNow => _initialTime + Stopwatch.Elapsed;

    /// <summary>
    /// Checks for clock drift between the monotonic time and system time.
    /// - If the system clock has changed by more than 1 minute, sets IsShutdownRequested to true.
    /// - If the change is less than 1 minute, adjusts the monotonic time to sync with the system clock.
    /// </summary>
    private static void CheckClockDrift()
    {
        var drift = DateTimeOffset.UtcNow - UtcNow;
        if (drift > TimeSpan.FromMinutes(1) || TimeSpan.FromMinutes(-1) > drift)
        {
            IsShutdownRequested = true;
            return;
        }

        if (drift > TimeSpan.Zero) // do nothing when drift < TimeSpan.Zero is true since the MonotonicTime only moves forward
        {
            var now = DateTimeOffset.UtcNow;
            Stopwatch.Restart();
            _initialTime = now; //todo: add sync lock to prevent inconsistent UtcNow between correction
        }
    }
}

public static class TimeStampManager
{
    private static DateTimeOffset _cachedUtcNow;
    private static long _cachedUtcNowTicks;
    internal static readonly int UpdatePeriod = 50;
    private static readonly Timer Timer;

    static TimeStampManager()
    {
        Timer = new Timer(_ => //todo Use a single-fire timer that reschedules itself to prevent overlapping callbacks.
        {
            var now = MonotonicSystemHybridDateTime.UtcNow.Ticks;
            var secondResidue = now % TimeSpan.TicksPerSecond;
            _cachedUtcNow = new DateTimeOffset(now - secondResidue, TimeSpan.Zero);
            _cachedUtcNowTicks = _cachedUtcNow.Ticks;
        }, null, 0, UpdatePeriod);
    }

    /// <summary>
    /// Cached UTC timestamp with precision up to the second.
    /// This value is updated periodically and does not include milliseconds or finer precision.
    /// </summary>
    public static DateTimeOffset UtcNow => _cachedUtcNow;

    public static long UtcNowTicks => _cachedUtcNowTicks;

    /// <summary>
    /// Computes the current timestamp as an integer, representing the number of seconds elapsed since the specified epoch.
    /// </summary>
    /// <param name="epoch">The reference time (epoch) from which the elapsed seconds are calculated.</param>
    /// <returns>The number of seconds elapsed since the given epoch.</returns>
    public static long CurrentTimestamp(DateTimeOffset epoch) => (UtcNowTicks - epoch.Ticks) / TimeSpan.TicksPerSecond;
}

public static class SequenceManager<TEntity> where TEntity : class
{
    private static DateTimeOffset _epoch = IdGenerator.Epoch2025;
    private static SequenceTimeScope _timeScope = new(TimeStampManager.CurrentTimestamp(IdGenerator.Epoch2025));

    public static SequenceTimeScopedId GetTimeScopedId()
    {
        var timeStamp = TimeStampManager.CurrentTimestamp(_epoch);

        if (_timeScope.ScopeTimestamp != timeStamp)
            UpdateTimeScope();

        if (_timeScope.TryGetNextId(out var sequenceId))
            return new SequenceTimeScopedId(_timeScope.ScopeTimestamp, sequenceId);

        while (true)
        {
            var newTimestamp = TimeStampManager.CurrentTimestamp(_epoch);
            if (timeStamp == newTimestamp)
            {
                Thread.Sleep(TimeStampManager.UpdatePeriod); // Prevent busy-waiting
                continue;
            }

            UpdateTimeScope();
            if (_timeScope.TryGetNextId(out sequenceId))
                return new SequenceTimeScopedId(_timeScope.ScopeTimestamp, sequenceId);
        }
    }

    private static void UpdateTimeScope()
    {
        var newTimestamp = TimeStampManager.CurrentTimestamp(_epoch);
        var currentScope = _timeScope;
        if (currentScope.ScopeTimestamp == newTimestamp) return;

        var newScope = new SequenceTimeScope(newTimestamp);
        while (true)
        {
            Interlocked.CompareExchange(ref _timeScope, newScope, currentScope);
            if (_timeScope == newScope)
                break;

            currentScope = _timeScope; // Another thread updated _timeScope; check if it matches our target
            if (currentScope.ScopeTimestamp == TimeStampManager.CurrentTimestamp(_epoch))
                break;

            newScope = new SequenceTimeScope(TimeStampManager.CurrentTimestamp(_epoch)); // Retry with the new current scope
        }
    }
}

public readonly record struct SequenceTimeScopedId(long TimeStamp, ushort SequenceId);

public class SequenceTimeScope(long scopeTimeStamp)
{
    private int _lastId = -1;
    public long ScopeTimestamp { get; } = scopeTimeStamp;

    public bool TryGetNextId(out ushort id)
    {
        var nextId = Interlocked.Increment(ref _lastId);
        if (nextId <= ushort.MaxValue)
        {
            id = (ushort)nextId;
            return true;
        }

        id = 0;
        return false;
    }
}