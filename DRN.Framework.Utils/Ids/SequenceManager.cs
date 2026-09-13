using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Ids;

/// <summary>
/// Manages entity-specific sequences to generate unique, time-scoped identifiers in a thread-safe manner.
/// </summary>
/// <typeparam name="TEntity">The entity type for which sequences are managed. Must be a reference type.</typeparam>
/// <remarks>
/// Atomic sequences are scoped to cached timestamps relative to the frozen process epoch.
/// </remarks>
[SuppressMessage("ReSharper", "StaticMemberInGenericType")]
public static class SequenceManager<TEntity> where TEntity : class
{
    // ReSharper disable once StaticMemberInGenericType
    [SuppressMessage("SonarQube", "S2743", Justification = "Each entity type intentionally owns an independent timestamp scope and sequence.")]
    private static volatile SequenceTimeScope _timeScope = new(-1);
    private static readonly Lock ScopeLock = new();

    /// <summary>
    /// Generates a new time-scoped identifier for the entity type.
    /// </summary>
    /// <remarks>
    /// Exhausted buckets wait for the next cached timestamp. Concurrent calls may complete out of allocation order.
    /// </remarks>
    public static SequenceTimeScopedId GetTimeScopedId()
    {
        var currentScope = GetCurrentScope();

        return currentScope.TryGetNextId(out var sequenceId)
            ? new SequenceTimeScopedId(currentScope.ScopeTimestamp, sequenceId)
            : WaitForNextId();
    }

    private static SequenceTimeScopedId WaitForNextId()
    {
        while (true)
        {
            Thread.Sleep(TimeStampManager.UpdatePeriod);

            var currentScope = GetCurrentScope();
            if (currentScope.TryGetNextId(out var sequenceId))
                return new SequenceTimeScopedId(currentScope.ScopeTimestamp, sequenceId);
        }
    }

    private static SequenceTimeScope GetCurrentScope()
    {
        // Retain this exact scope so its sequence cannot be paired with another timestamp.
        var currentScope = _timeScope;
        return currentScope.ScopeTimestamp == TimeStampManager.CurrentTimestamp() ? currentScope : AdvanceScope();
    }

    private static SequenceTimeScope AdvanceScope()
    {
        lock (ScopeLock)
        {
            // Re-read after waiting: another caller may have already published a newer bucket.
            var timestamp = TimeStampManager.CurrentTimestamp();
            if (_timeScope.ScopeTimestamp != timestamp)
                _timeScope = new SequenceTimeScope(timestamp);

            return _timeScope;
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
        var nextId = unchecked((uint)Interlocked.Increment(ref _lastId));
        if (nextId <= MaxValue)
        {
            id = nextId;
            return true;
        }

        id = 0;
        return false;
    }
}
