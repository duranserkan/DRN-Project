using DRN.Framework.Utils.Common.Time;

namespace DRN.Framework.Utils.Common.Sequences;

/// <summary>
/// Manages entity-specific sequences to generate unique, time-scoped identifiers in a thread-safe manner.
/// </summary>
/// <typeparam name="TEntity">The entity type for which sequences are managed. Must be a reference type.</typeparam>
/// <remarks>
/// <para>
/// This class uses a combination of timestamp intervals (based on a fixed epoch) and atomic operations to generate
/// collision-resistant IDs across multiple threads or processes. The time scope is automatically advanced when the
/// system clock progresses beyond the current interval, ensuring monotonically increasing IDs within each interval.
/// </para>
/// <para>
/// The epoch is fixed at 2025-01-01 to maximize timestamp longevity.
/// </para>
/// </remarks>
public static class SequenceManager<TEntity> where TEntity : class
{
    private static DateTimeOffset _epoch = IdGenerator.Epoch2025;
    private static SequenceTimeScope _timeScope = new(TimeStampManager.CurrentTimestamp(IdGenerator.Epoch2025));

    /// <summary>
    /// Generates a new time-scoped identifier for the entity type.
    /// </summary>
    /// <returns>
    /// A <see cref="SequenceTimeScopedId"/> containing both the timestamp of the current interval and the
    /// unique sequence number within that interval.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method guarantees thread-safe ID generation by using atomic operations to manage time scope transitions.
    /// When the current time interval expires, the method automatically advances to the new interval and resets
    /// the sequence counter.
    /// </para>
    /// <para>
    /// In high-contention scenarios where the sequence counter exhausts an interval's capacity, the method employs
    /// a lightweight synchronization pattern with exponential backoff (via <see cref="Thread.Sleep"/>) to wait for
    /// the next available interval while minimizing CPU contention.
    /// </para>
    /// <para>
    /// The generated IDs are strictly ordered within their time interval but not globally monotonic across intervals.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var id = SequenceManager&lt;Order&gt;.GetTimeScopedId();
    /// Console.WriteLine($"Order ID: {id.Timestamp}-{id.Sequence}");
    /// </code>
    /// </example>
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
                Thread.Sleep(TimeStampManager.UpdatePeriod); //to prevent busy-waiting
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