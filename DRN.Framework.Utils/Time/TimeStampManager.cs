namespace DRN.Framework.Utils.Time;

/// <summary>
/// Provides cached UTC timestamps with second-level precision, updated periodically.
/// </summary>
/// <remarks>
/// <para>
/// <b>Drift Compensation Strategy — Freeze and Ride-Through</b><br/>
/// When a minor backward clock drift is detected (less than <see cref="MaxAllowedDriftSeconds"/> seconds),
/// the cached timestamp is <b>not</b> updated. <see cref="UtcNowTicks"/> continues returning the last
/// known-good (higher) value until the real system clock catches up naturally. There is no spin-wait,
/// sleep, or blocking — the stale value is served transparently.
/// </para>
/// <para>
/// This is safe because downstream consumers such as <c>SequenceManager&lt;TEntity&gt;</c> tolerate repeated
/// timestamps: they use per-second atomic sequence counters (up to 1,048,575 IDs per second) and only
/// require the timestamp to <i>not go backward</i>, which the freeze guarantees.
/// </para>
/// <para>
/// If the backward drift equals or exceeds <see cref="MaxAllowedDriftSeconds"/> seconds, the drift is
/// considered critical: a <see cref="ClockDriftException"/> flag is set and application shutdown is
/// requested via <see cref="ApplicationLifetime.RequestShutdown"/>.
/// </para>
/// </remarks>
public static class TimeStampManager
{
    private static long _cachedUtcNowTicks;

    /// <summary>Timer period in milliseconds between the end of one update and the start of the next.</summary>
    internal static readonly int UpdatePeriod = 10;

    internal static readonly int MaxAllowedDriftSeconds = 3;
    private static int _driftDetected; // 0 = normal, 1 = drift detected
    private static ClockDriftException? _driftException;
    private static readonly RecurringAction RecurringAction = new(GetUpdateAction(), UpdatePeriod);

    private static Func<Task> GetUpdateAction()
    {
        _ = RecurringAction; //this action used to trigger updates
        Update();
        return Update;
    }

    private static Task Update()
    {
        var now = DateTimeProvider.UtcNow.Ticks;
        var secondResidue = now % TimeSpan.TicksPerSecond;
        var truncatedNow = now - secondResidue;
        var previousTicks = Volatile.Read(ref _cachedUtcNowTicks);

        if (truncatedNow < previousTicks)
        {
            var driftTicks = previousTicks - truncatedNow;
            var driftSeconds = driftTicks / TimeSpan.TicksPerSecond;

            if (driftSeconds >= MaxAllowedDriftSeconds)
            {
                // Critical drift: cache exception, set flag, request shutdown
                _driftException = new ClockDriftException(previousTicks, truncatedNow);
                Volatile.Write(ref _driftDetected, 1);
                ApplicationLifetime.RequestShutdown();
                return Task.CompletedTask;
            }

            // Minor drift (< MaxAllowedDriftSeconds): freeze the cached value.
            // UtcNowTicks will continue serving the previous (higher) timestamp until
            // the real clock catches up. This is safe because downstream consumers
            // (e.g. SequenceManager) use per-second sequence counters and only require
            // timestamps to never go backward — which the freeze guarantees.
            return Task.CompletedTask;
        }

        Volatile.Write(ref _cachedUtcNowTicks, truncatedNow);
        return Task.CompletedTask;
    }

    public static long UtcNowTicks => Volatile.Read(ref _driftDetected) != 1
        ? Volatile.Read(ref _cachedUtcNowTicks)
        : throw _driftException!;

    /// <summary>
    /// Cached UTC timestamp with precision up to the second.
    /// This value is updated periodically and does not include milliseconds or finer precision.
    /// </summary>
    /// <exception cref="ClockDriftException">Thrown when a critical clock drift has been detected.</exception>
    public static DateTimeOffset UtcNow => new(UtcNowTicks, TimeSpan.Zero);

    /// <summary>
    /// Computes the current timestamp as an integer, representing the number of seconds elapsed since the specified epoch.
    /// </summary>
    /// <param name="epoch">The reference time (epoch) from which the elapsed seconds are calculated.</param>
    /// <returns>The number of seconds elapsed since the given epoch.</returns>
    /// <exception cref="ClockDriftException">Thrown when a critical clock drift has been detected.</exception>
    public static long CurrentTimestamp(DateTimeOffset epoch) => (UtcNowTicks - epoch.Ticks) / TimeSpan.TicksPerSecond;
}