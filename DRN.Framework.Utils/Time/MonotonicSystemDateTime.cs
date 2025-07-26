using System.Diagnostics;
using DRN.Framework.SharedKernel.Utils;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Time;

[Singleton<ISystemDateTimeProvider>]
public class SystemDateTimeProvider : ISystemDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

[Singleton<IDateTimeProvider>]
public class MonotonicSystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => MonotonicSystemDateTime.UtcNow;
}

/// <summary>
/// Monotonic/System hybrid clock that is immune to drastic system clock changes and monotonic clock drifts
/// </summary>
public static class MonotonicSystemDateTime
{
    private const int UpdatePeriod = 10000; // 10 seconds

    private static readonly DateTimeProviderInstance ProviderInstance = new(new SystemDateTimeProvider(), UpdatePeriod);

    internal static event Action<DriftInfo> OnDriftCorrected
    {
        add => ProviderInstance.OnDriftCorrected += value;
        remove => ProviderInstance.OnDriftCorrected -= value;
    }

    /// <summary>
    /// Application should poll this flag to verify consistency
    /// </summary>
    public static bool IsShutdownRequested => ProviderInstance.IsShutdownRequested;

    public static RecurringAction RecurringAction => ProviderInstance.RecurringAction;

    /// <summary>
    /// Gets the current UTC time using a monotonic clock (Stopwatch).
    /// Immune to system clock changes (e.g., NTP adjustments).
    /// </summary>
    public static DateTimeOffset UtcNow => ProviderInstance.UtcNow;

    /// <summary>
    /// Disposes the resources used by the MonotonicSystemDateTime.
    /// </summary>
    internal static void Dispose() => ProviderInstance.Dispose();
}

/// <summary>
/// Monotonic/System hybrid clock that is immune to drastic system clock changes and monotonic clock drifts
/// </summary>
public class DateTimeProviderInstance
{
    private static readonly TimeSpan Minute1 = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinuteMinus1 = TimeSpan.FromMinutes(-1);
    private static readonly TimeSpan GracePeriodUpper = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan GracePeriodLower = TimeSpan.FromMilliseconds(-50);
    private static readonly TimeSpan Millisecond = TimeSpan.FromMilliseconds(1);
    private static readonly int MaxSpinCount = 5;

    private readonly ISystemDateTimeProvider _timeProviderProvider;


    //TimeState is being updated regularly with a new instance for small drifts.
    //Since initial time and stopwatch are stored together, small drifts can be tolerated for dirty reads.
    //For drastic changes the app shuts itself for a restart to get new app instance id, then it is no longer a problem
    private volatile TimeState _timeState;
    private volatile bool _isShutdownRequested;

    /// <summary>
    /// Application should poll this flag to verify consistency
    /// </summary>
    public bool IsShutdownRequested => _isShutdownRequested;

    internal event Action<DriftInfo>? OnDriftCorrected;
    internal event Action<DriftInfo>? OnDriftChecked;
    public RecurringAction RecurringAction { get; private set; }

    public DateTimeProviderInstance(ISystemDateTimeProvider timeProviderProvider, int updatePeriod)
    {
        _timeProviderProvider = timeProviderProvider;
        _timeState = UpdateTimeState();
        RecurringAction = new RecurringAction(CheckClockDrift, updatePeriod);
    }

    private long _lastReturned;

    //todo: benchmark Interlocked.Increment vs Interlocked.CompareExchange
    /// <summary>
    /// Gets the current UTC time using a monotonic clock (Stopwatch).
    /// Immune to system clock changes (e.g., NTP adjustments).
    /// </summary>
    public DateTimeOffset UtcNow
    {
        //Ensure strict monotonicity
        get
        {
            long actual;
            var candidate = _timeState.UtcNowTicks;

            // Fast path: if the candidate is already ahead, try direct CAS
            var previous = Volatile.Read(ref _lastReturned);
            if (candidate > previous) //we call it previous because it may be changed already
            {
                actual = Interlocked.CompareExchange(ref _lastReturned, candidate, previous);
                if (actual == previous)
                    return new DateTimeOffset(candidate, TimeSpan.Zero);
                if (actual > candidate) // the last value can be used with an increment
                    return new DateTimeOffset(Interlocked.Increment(ref _lastReturned), TimeSpan.Zero);
            }
            else // bump forward if it would go backward or stay the same
                return new DateTimeOffset(Interlocked.Increment(ref _lastReturned), TimeSpan.Zero);

            // Contended path: use spin-wait with backoff
            var spinCount = 0;
            var spinner = new SpinWait();
            do
            {
                // Exponential backoff to reduce contention
                if (spinCount > 0)
                    if (spinCount < MaxSpinCount)
                        for (var i = 0; i < spinCount * 2; i++)
                            spinner.SpinOnce();
                    else
                        Thread.Yield(); // Fallback to thread yield after max spins

                candidate = _timeState.UtcNowTicks;
                previous = Volatile.Read(ref _lastReturned);

                if (previous >= candidate) // bump forward if it would go backward or stay the same
                    return new DateTimeOffset(Interlocked.Increment(ref _lastReturned), TimeSpan.Zero);

                actual = Interlocked.CompareExchange(ref _lastReturned, candidate, previous);
                if (actual > candidate) // the last value can be used with an increment
                    return new DateTimeOffset(Interlocked.Increment(ref _lastReturned), TimeSpan.Zero);

                spinCount++;
            } while (actual != previous);

            return new DateTimeOffset(candidate, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Checks for clock drift between the monotonic time and system time.
    /// - If the system clock has changed by more than 1 minute, sets IsShutdownRequested to true.
    /// - If the change is less than 1 minute, adjusts the monotonic time to sync with the system clock.
    /// </summary>
    private async Task CheckClockDrift()
    {
        //When positive it means the monotonic clock is lagged behind the actual value
        //When negative it means the system clock is lagged behind the actual value
        var systemTime = _timeProviderProvider.UtcNow;
        var monotonicSystemTime = UtcNow;
        var drift = systemTime - monotonicSystemTime;

        //grace interval
        if (drift > GracePeriodLower && drift < GracePeriodUpper)
        {
            OnDriftChecked?.Invoke(new DriftInfo(systemTime, monotonicSystemTime, drift));
            return;
        }

        //long waits are handled by significant drift protection by shutdown request
        if (drift > Minute1 || drift < MinuteMinus1)
        {
            _isShutdownRequested = true;
            OnDriftChecked?.Invoke(new DriftInfo(systemTime, monotonicSystemTime, drift));
            return;
        }

        // The MonotonicTime only moves forward even if it is not strict in this implementation
        // When drift is negative and less than 1 minute, the monotonic clock waits to catch system clock
        // When drift is positive, the monotonic clock is behind the system clock and can catch system clock directly, 
        if (drift <= TimeSpan.Zero)
            await Task.Delay(drift * -1 + Millisecond);

        // The MonotonicTime only moves forward
        // When drift is negative and less than 1 minute, the monotonic clock reads increment last returned to catch up.
        // When drift is positive, the monotonic clock is behind the system clock and can catch system clock directly,
        // It is ok to update TimeState regardless of drift direction 
        UpdateTimeState();

        OnDriftCorrected?.Invoke(new DriftInfo(systemTime, monotonicSystemTime, drift));
        OnDriftChecked?.Invoke(new DriftInfo(systemTime, monotonicSystemTime, drift));
    }

    private TimeState UpdateTimeState()
    {
        var stopwatch = new Stopwatch();
        var now = _timeProviderProvider.UtcNow;
        stopwatch.Restart();
        _timeState = new TimeState(now.Ticks, stopwatch);

        return _timeState;
    }

    /// <summary>
    /// Disposes the resources used by the MonotonicSystemDateTime.
    /// </summary>
    public void Dispose() => RecurringAction.Dispose(); //disposing only stops drifting check, not stop the clock itself
}

public record DriftInfo(DateTimeOffset SystemDateTime, DateTimeOffset MonotonicSystemDateTime, TimeSpan Drift);

public class TimeState(long initialTimeTicks, Stopwatch stopwatch)
{
    public long UtcNowTicks => initialTimeTicks + stopwatch.Elapsed.Ticks;
}