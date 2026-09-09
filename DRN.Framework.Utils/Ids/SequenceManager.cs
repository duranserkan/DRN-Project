using DRN.Framework.Utils.Concurrency;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Ids;

/// <summary>
/// Manages entity-specific sequences to generate unique, time-scoped identifiers in a thread-safe manner.
/// </summary>
/// <typeparam name="TEntity">The entity type for which sequences are managed. Must be a reference type.</typeparam>
/// <remarks>
/// Atomic sequences are scoped to cached UTC buckets and encoded relative to the frozen process epoch.
/// </remarks>
public static class SequenceManager<TEntity> where TEntity : class
{
    // ReSharper disable once StaticMemberInGenericType
    private static volatile SequenceTimeScope _timeScope = new(-1);

    /// <summary>
    /// Generates a new time-scoped identifier for the entity type.
    /// </summary>
    /// <remarks>
    /// Exhausted buckets wait for the next cached timestamp. Concurrent calls may complete out of allocation order.
    /// </remarks>
    public static SequenceTimeScopedId GetTimeScopedId()
    {
        var epochTicks = EpochTimeUtils.DefaultEpoch.UtcTicks;
        var currentScope = GetCurrentScope(epochTicks);
        while (true)
        {
            if (currentScope.TryGetNextId(out var sequenceId))
                return new SequenceTimeScopedId(
                    (currentScope.ScopeTimestamp - epochTicks) / TimeStampManager.TicksPerPrecisionUnit, sequenceId);

            Thread.Sleep(TimeStampManager.UpdatePeriod);
            currentScope = GetCurrentScope(epochTicks);
        }
    }

    private static SequenceTimeScope GetCurrentScope(long epochUtcTicks)
    {
        while (true)
        {
            // Retain this exact UTC scope after validation;
            // another caller may advance the global scope before this caller allocates its ID.
            var currentScope = _timeScope;
            var utcTicks = TimeStampManager.GetGenerationUtcTicks(epochUtcTicks);
            if (currentScope.ScopeTimestamp == utcTicks)
                return currentScope;

            var newScope = new SequenceTimeScope(utcTicks);
#pragma warning disable CS0420 // Interlocked provides full memory barrier
            if (LockUtils.TrySetIfEqual(ref _timeScope, newScope, currentScope))
                return newScope;
#pragma warning restore CS0420
        }
    }
}

public readonly record struct SequenceTimeScopedId(long TimeStamp, uint SequenceId);

public class SequenceTimeScope(long scopeTimeStamp)
{
    public const uint MaxValue = 262143;
    public const uint MinValue = 0;

    private int _lastId = -1;
    public long ScopeTimestamp { get; } = scopeTimeStamp;

    public bool TryGetNextId(out uint id)
    {
        var nextId = Interlocked.Increment(ref _lastId);
        if (nextId <= MaxValue)
        {
            id = (uint)nextId;
            return true;
        }

        id = 0;
        return false;
    }
}
