using System.Diagnostics;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Time;

/// <summary>
/// Monotonic/System hybrid clock that is immune to drastic system clock changes and monotonic clock drifts
/// </summary>
public static class MonotonicSystemDateTime
{
    private const int UpdatePeriod = 10000; // 10 seconds

    internal static readonly MonotonicSystemDateTimeInstance Instance = new(new DateTimeProvider(), UpdatePeriod);

    internal static event Action<DriftInfo> OnDriftCorrected
    {
        add => Instance.OnDriftCorrected += value;
        remove => Instance.OnDriftCorrected -= value;
    }

    /// <summary>
    /// Application should poll this flag to verify consistency
    /// </summary>
    public static bool IsShutdownRequested => Instance.IsShutdownRequested;

    public static RecurringAction RecurringAction => Instance.RecurringAction;

    /// <summary>
    /// Gets the current UTC time using a monotonic clock (Stopwatch).
    /// Immune to system clock changes (e.g., NTP adjustments).
    /// </summary>
    public static DateTimeOffset UtcNow => Instance.UtcNow;

    /// <summary>
    /// Disposes the resources used by the MonotonicSystemDateTime.
    /// </summary>
    internal static void Dispose() => Instance.Dispose();
}

public interface ISystemDateTime
{
    DateTimeOffset UtcNow { get; }
}

//MonotonicSystemDateTimeInstance is registered as a service to ioc container with utils module without an attribute
internal interface IMonotonicSystemDateTime : ISystemDateTime;

[Singleton<ISystemDateTime>]
public class DateTimeProvider : ISystemDateTime
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Monotonic/System hybrid clock that is immune to drastic system clock changes and monotonic clock drifts
/// </summary>
public class MonotonicSystemDateTimeInstance : IMonotonicSystemDateTime
{
    private readonly TimeSpan _gracePeriodUpper = TimeSpan.FromMilliseconds(50);
    private readonly TimeSpan _gracePeriodLower = TimeSpan.FromMilliseconds(-50);

    private DateTimeOffset _initialTime;
    private volatile bool _isShutdownRequested;

    private readonly Lock _syncLock = new(); // Synchronization for high-frequency reads and low frequency writes
    private readonly Stopwatch _stopwatch;
    private readonly ISystemDateTime _timeProvider;


    /// <summary>
    /// Application should poll this flag to verify consistency
    /// </summary>
    public bool IsShutdownRequested => _isShutdownRequested;

    internal event Action<DriftInfo>? OnDriftCorrected;
    internal event Action<DriftInfo>? OnDriftChecked;
    public RecurringAction RecurringAction { get; private set; }

    public MonotonicSystemDateTimeInstance(ISystemDateTime timeProvider, int updatePeriod)
    {
        _timeProvider = timeProvider;
        _initialTime = _timeProvider.UtcNow;
        _stopwatch = Stopwatch.StartNew();

        RecurringAction = new RecurringAction(CheckClockDriftAsync, updatePeriod);
    }

    /// <summary>
    /// Gets the current UTC time using a monotonic clock (Stopwatch).
    /// Immune to system clock changes (e.g., NTP adjustments).
    /// </summary>
    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_syncLock)
                return _initialTime + _stopwatch.Elapsed; // Atomic read of both values
        }
    }

    /// <summary>
    /// Checks for clock drift between the monotonic time and system time.
    /// - If the system clock has changed by more than 1 minute, sets IsShutdownRequested to true.
    /// - If the change is less than 1 minute, adjusts the monotonic time to sync with the system clock.
    /// </summary>
    private async Task CheckClockDriftAsync()
    {
        //When positive it means the monotonic clock is lagged behind the actual value
        //When negative it means the system clock is lagged behind the actual value
        var systemTime = _timeProvider.UtcNow;
        var monotonicSystemTime = UtcNow;
        var drift = systemTime - monotonicSystemTime;

        //grace interval
        if (drift > _gracePeriodLower && drift < _gracePeriodUpper)
        {
            OnDriftChecked?.Invoke(new DriftInfo(systemTime, monotonicSystemTime, drift));
            return;
        }


        //long waits are handled by significant drift protection by shutdown request
        if (drift > TimeSpan.FromMinutes(1) || TimeSpan.FromMinutes(-1) > drift)
        {
            _isShutdownRequested = true;
            OnDriftChecked?.Invoke(new DriftInfo(systemTime, monotonicSystemTime, drift));
            return;
        }

        // The MonotonicTime only moves forward even if it is not strict in this implementation
        // When drift is negative and less than 1 minute, the monotonic clock waits to catch system clock
        // When drift is positive, the monotonic clock is behind the system clock and can catch system clock directly, 
        if (drift <= TimeSpan.Zero)
            await Task.Delay(drift * -1 + TimeSpan.FromMicroseconds(2));

        lock (_syncLock) // Atomic write of both values
        {
            _stopwatch.Restart();
            _initialTime = _timeProvider.UtcNow;
        }

        OnDriftCorrected?.Invoke(new DriftInfo(systemTime, monotonicSystemTime, drift));
        OnDriftChecked?.Invoke(new DriftInfo(systemTime, monotonicSystemTime, drift));
    }

    /// <summary>
    /// Disposes the resources used by the MonotonicSystemDateTime.
    /// </summary>
    public void Dispose() => RecurringAction.Dispose(); //disposing is will only stop drifting check, not stop the clock itself
}

public record DriftInfo(DateTimeOffset SystemDateTime, DateTimeOffset MonotonicSystemDateTime, TimeSpan Drift);