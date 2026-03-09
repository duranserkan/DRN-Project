namespace DRN.Framework.Utils.Time;

/// <summary>
/// Thrown when <see cref="TimeStampManager"/> detects a backward clock drift exceeding
/// <see cref="TimeStampManager.MaxAllowedDriftSeconds"/> seconds. Once thrown, no further
/// timestamps will be generated until the application restarts.
/// </summary>
public class ClockDriftException : Exception
{
    public long PreviousTicks { get; }
    public long NewTicks { get; }
    public double DriftSeconds { get; }

    public ClockDriftException(long previousTicks, long newTicks)
        : this(previousTicks, newTicks, ComputeDriftSeconds(previousTicks, newTicks))
    {
    }

    private ClockDriftException(long previousTicks, long newTicks, double driftSeconds)
        : base(FormatMessage(previousTicks, newTicks, driftSeconds))
    {
        PreviousTicks = previousTicks;
        NewTicks = newTicks;
        DriftSeconds = driftSeconds;
    }

    private static double ComputeDriftSeconds(long previousTicks, long newTicks) =>
        (double)(previousTicks - newTicks) / TimeSpan.TicksPerSecond;

    private static string FormatMessage(long previousTicks, long newTicks, double driftSeconds) =>
        $"Critical clock drift detected: system clock moved backward by {driftSeconds:F1}s " +
        $"(previous: {new DateTimeOffset(previousTicks, TimeSpan.Zero):O}, " +
        $"current: {new DateTimeOffset(newTicks, TimeSpan.Zero):O}). " +
        "Application shutdown initiated. No further timestamps will be generated.";
}
