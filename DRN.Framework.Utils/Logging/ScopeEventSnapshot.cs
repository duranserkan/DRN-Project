namespace DRN.Framework.Utils.Logging;

// Event objects are immutable. The additional-event array is detached from scoped storage.
internal readonly record struct ScopeEventSnapshot(ScopeEvent? Primary, ScopeEvent[]? Additional, long OmittedCount = 0)
{
    internal const int MaxAdditionalEvents = 8;
    private const string OmittedCountKey = "OmittedEventCount";

    internal void WriteTo(IDictionary<string, object> output)
    {
        if (Primary is { } primary)
        {
            output[ScopedLogConventions.KeyOfEventId] = primary.Id.Id;
            output[ScopedLogConventions.KeyOfEventName] = primary.Id.Name ?? string.Empty;
            output[ScopedLogConventions.KeyOfEventOutcome] = primary.Outcome ?? string.Empty;
            output[ScopedLogConventions.KeyOfEventReason] = primary.Reason ?? string.Empty;
        }
        if (Additional is { Length: > 0 })
            output[ScopedLogConventions.KeyOfAdditionalEvents] = new List<object>(Additional);
        // Event accounting is authoritative and is attached after diagnostic bounds and sanitization.
        output.Remove(OmittedCountKey);
        if (OmittedCount > 0) output[OmittedCountKey] = OmittedCount;
    }

    // Only typed events carry event authority. Ordinary event-shaped diagnostics remain protected.
    internal static ScopeEventSnapshot TakeFrom(ScopeEvent? primary, Dictionary<string, object> diagnostics)
    {
        if (primary != null)
        {
            diagnostics.Remove(ScopedLogConventions.KeyOfEventId);
            diagnostics.Remove(ScopedLogConventions.KeyOfEventName);
            diagnostics.Remove(ScopedLogConventions.KeyOfEventOutcome);
            diagnostics.Remove(ScopedLogConventions.KeyOfEventReason);
        }
        ScopeEvent[]? additional = null;
        if (diagnostics.TryGetValue(ScopedLogConventions.KeyOfAdditionalEvents, out var value))
        {
            if (value is IReadOnlyList<ScopeEvent> events) additional = events.Take(MaxAdditionalEvents).ToArray();
            else if (value is IList<object> items && items.All(item => item is ScopeEvent))
                additional = items.Take(MaxAdditionalEvents).Cast<ScopeEvent>().ToArray();
            if (additional != null) diagnostics.Remove(ScopedLogConventions.KeyOfAdditionalEvents);
        }
        long omitted = value switch
        {
            IReadOnlyList<ScopeEvent> events => Math.Max(0, events.Count - MaxAdditionalEvents),
            IList<object> items when additional != null => Math.Max(0, items.Count - MaxAdditionalEvents),
            _ => 0
        };
        if (diagnostics.Remove(OmittedCountKey, out var count))
        {
            var reported = count switch { long number when number > 0 => number, int number when number > 0 => number, _ => 0L };
            omitted += Math.Min(reported, long.MaxValue - omitted);
        }
        return new ScopeEventSnapshot(primary, additional, omitted);
    }
}
