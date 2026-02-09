namespace DRN.Framework.Utils.Time;

public static class TimeStampManager
{
    private static long _cachedUtcNowTicks;
    internal static readonly int UpdatePeriod = 10;
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
        Volatile.Write(ref _cachedUtcNowTicks, now - secondResidue);
        return Task.CompletedTask;
    }
    
    public static long UtcNowTicks => Volatile.Read(ref _cachedUtcNowTicks);
    
    /// <summary>
    /// Cached UTC timestamp with precision up to the second.
    /// This value is updated periodically and does not include milliseconds or finer precision.
    /// </summary>
    public static DateTimeOffset UtcNow => new(UtcNowTicks, TimeSpan.Zero);
    
    /// <summary>
    /// Computes the current timestamp as an integer, representing the number of seconds elapsed since the specified epoch.
    /// </summary>
    /// <param name="epoch">The reference time (epoch) from which the elapsed seconds are calculated.</param>
    /// <returns>The number of seconds elapsed since the given epoch.</returns>
    public static long CurrentTimestamp(DateTimeOffset epoch) => (UtcNowTicks - epoch.Ticks) / TimeSpan.TicksPerSecond;
}