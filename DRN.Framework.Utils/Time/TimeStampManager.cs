using DRN.Framework.SharedKernel.Domain;

namespace DRN.Framework.Utils.Time;

/// <summary>
/// Provides cached UTC timestamps with 250ms precision (4 ticks per second), updated periodically.
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
/// timestamps: they use per-tick atomic sequence counters (up to 262,143 IDs per 250ms tick) and only
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
    
    public const int PrecisionUnitInMsSafeDelay = 260;
    public const int PrecisionUnitInMs = 250;

    /// <summary>Number of precision ticks per second (4 ticks/s for 250ms precision).</summary>
    public const int TicksPerSecondMultiplier = 1000 / PrecisionUnitInMs;

    /// <summary>Number of .NET ticks per precision unit (250ms = 2,500,000 ticks).</summary>
    public const long TicksPerPrecisionUnit = TimeSpan.TicksPerSecond / TicksPerSecondMultiplier;


    /// <summary>Timer period in milliseconds between the end of one update and the start of the next.</summary>
    internal const int UpdatePeriod = 10;
    internal const int MaxAllowedDriftSeconds = 5;

    private static int _driftDetected; // 0 = normal, 1 = drift detected
    private static ClockDriftException? _driftException;
    private static readonly GenerationTimeInitialization GenerationInitialization = new(
        static () => SourceKnownGenerationTime.ForGeneration, static () => UtcNow);
    private static readonly RecurringAction RecurringAction = new(
        Update, UpdatePeriod, threadName: "DRN.TimeStampManager", priority: ThreadPriority.Highest);

    static TimeStampManager() => Update();

    private static void Update()
    {
        var now = DateTimeProvider.UtcNow.Ticks;
        var precisionResidue = now % TicksPerPrecisionUnit;
        var truncatedNow = now - precisionResidue;
        var previousTicks = Volatile.Read(ref _cachedUtcNowTicks);

        try
        {
            Volatile.Write(ref _cachedUtcNowTicks, GetUpdatedTicks(previousTicks, truncatedNow));
        }
        catch (ClockDriftException exception)
        {
            _driftException = exception;
            Volatile.Write(ref _driftDetected, 1);
            RecurringAction.Stop();
            ApplicationLifetime.RequestShutdown();
        }
    }

    internal static long GetUpdatedTicks(long previousTicks, long truncatedNow)
    {
        if (truncatedNow >= previousTicks)
            return truncatedNow;
        if ((previousTicks - truncatedNow) / TimeSpan.TicksPerSecond >= MaxAllowedDriftSeconds)
            throw new ClockDriftException(previousTicks, truncatedNow);
        return previousTicks;
    }

    public static long UtcNowTicks => Volatile.Read(ref _driftDetected) != 1
        ? Volatile.Read(ref _cachedUtcNowTicks)
        : throw _driftException!;

    /// <summary>
    /// Cached UTC timestamp with 250ms precision.
    /// This value is updated periodically and is truncated to the nearest 250ms boundary.
    /// </summary>
    /// <exception cref="ClockDriftException">Thrown when a critical clock drift has been detected.</exception>
    public static DateTimeOffset UtcNow => new(UtcNowTicks, TimeSpan.Zero);

    /// <summary>
    /// Computes the number of 250ms ticks elapsed since the configured process epoch.
    /// </summary>
    /// <returns>The number of 250ms ticks elapsed since the given epoch.</returns>
    /// <exception cref="ClockDriftException">Thrown when a critical clock drift has been detected.</exception>
    public static long CurrentTimestamp()
    {
        var epochTicks = EpochTimeUtils.DefaultEpoch.UtcTicks;
        return (GetGenerationUtcTicks(epochTicks) - epochTicks) / TicksPerPrecisionUnit;
    }

    /// <summary>
    /// Freezes the configured epoch and minimum and validates cached time once.
    /// Subsequent generation relies on the cache never decreasing. Failed validation can be retried.
    /// </summary>
    public static void InitializeGeneration()
        => GenerationInitialization.EnsureInitialized();

    internal static long GetGenerationUtcTicks(long epochUtcTicks)
    {
        InitializeGeneration();
        // Read after initialization: an earlier snapshot might precede the validated floor.
        var utcTicks = UtcNowTicks;
        ValidateEpochRange(utcTicks, epochUtcTicks);
        return utcTicks;
    }

    internal static void ValidateEpochRange(long utcTicks, long epochUtcTicks)
    {
        if ((ulong)(utcTicks - epochUtcTicks) >=
            (ulong)(SourceKnownGenerationTimePolicy.TimestampsPerEpoch * TicksPerPrecisionUnit))
            throw new InvalidOperationException("Generation time is outside the supported Source-Known epoch range.");
    }
}

/// <summary>Remembers the default-origin lower-bound proof for a frozen policy and a nondecreasing clock.</summary>
internal sealed class GenerationTimeInitialization(
    Func<SourceKnownGenerationTimePolicy> getPolicy, Func<DateTimeOffset> getCachedUtc)
{
    private readonly Lock _sync = new();
    private bool _initialized;

    internal void EnsureInitialized()
    {
        if (!Volatile.Read(ref _initialized))
            Initialize();
    }

    private void Initialize()
    {
        lock (_sync)
        {
            if (!_initialized)
            {
                var policy = getPolicy();
                policy.Validate(getCachedUtc());
                Volatile.Write(ref _initialized, true);
            }
        }
    }
}
