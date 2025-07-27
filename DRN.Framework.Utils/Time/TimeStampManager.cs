namespace DRN.Framework.Utils.Time;

public static class TimeStampManager
{
    private static DateTimeOffset _cachedUtcNow;
    private static long _cachedUtcNowTicks;
    internal static readonly int UpdatePeriod = 50;
    private static readonly RecurringAction RecurringAction = new RecurringAction(GetUpdateAction(), UpdatePeriod);
    
    private static Func<Task> GetUpdateAction()
    {
        Update();
        return Update;
    }

    private static Task Update()
    {
        var now = DateTimeProvider.UtcNow.Ticks;
        var secondResidue = now % TimeSpan.TicksPerSecond;
        _cachedUtcNow = new DateTimeOffset(now - secondResidue, TimeSpan.Zero);
        _cachedUtcNowTicks = _cachedUtcNow.Ticks;
        return Task.CompletedTask;
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