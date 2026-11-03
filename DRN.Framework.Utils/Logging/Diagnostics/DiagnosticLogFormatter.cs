using System.Text.Json;

namespace DRN.Framework.Utils.Logging.Diagnostics;

internal static class DiagnosticLogFormatter
{
    private const int LimitMarkerReserveBytes = 64;
    private const int MaxFormattingAttempts = 12;
    private const int MaxContextValueBytes = 256;
    private const string CaptureLimitedKey = "DiagnosticCaptureLimited";
    private const string PayloadFragmentKey = "PayloadFragment";
    private const string PayloadFragmentCountKey = "PayloadFragmentCount";
    private static readonly string[] ContextKeys =
    ["CorrelationId", "TraceId", "TraceIdentifier", "ClientError.ClientErrorId", "EventId", "EventName", "EventOutcome", "ExceptionType", "Response_StatusCode", "HttpMethod"];

    internal static Dictionary<string, object> BoundSnapshot(IReadOnlyDictionary<string, object> snapshot, int maximum)
    {
        using var sizing = DiagnosticJsonSize.Rent();
        return BoundSnapshot(snapshot, maximum, sizing);
    }

    private static Dictionary<string, object> BoundSnapshot(IReadOnlyDictionary<string, object> snapshot, int maximum,
        DiagnosticJsonSize sizing)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        var bytes = 2;
        var limited = false;
        foreach (var key in ContextKeys.Concat(snapshot.Keys).Distinct(StringComparer.Ordinal))
        {
            if (!snapshot.TryGetValue(key, out var value)) continue;
            var size = sizing.MeasureEntry(key, value);
            if (bytes + size > maximum - LimitMarkerReserveBytes) { limited = true; continue; }
            result[key] = value;
            bytes += size;
        }
        if (limited) result[CaptureLimitedKey] = true;
        return result;
    }

    internal static IReadOnlyList<Dictionary<string, object>> Create(IReadOnlyDictionary<string, object> snapshot,
        DiagnosticLogOptions options, ScopeEventSnapshot events = default)
    {
        var records = CreateDiagnostics(snapshot, options);
        // Trusted event metadata is outside diagnostic budgets and appears once, on the parent record.
        events.WriteTo(records[0]);
        return records;
    }

    private static IReadOnlyList<Dictionary<string, object>> CreateDiagnostics(IReadOnlyDictionary<string, object> snapshot, DiagnosticLogOptions options)
    {
        using var sizing = DiagnosticJsonSize.Rent();
        var group = Guid.NewGuid().ToString("N");
        var budget = options.MaxScopeBytes;
        for (var attempt = 0; attempt < MaxFormattingAttempts; attempt++, budget /= 2)
        {
            var bounded = BoundSnapshot(snapshot, budget, sizing);
            if (attempt > 0) bounded[CaptureLimitedKey] = true;
            var single = new Dictionary<string, object>(bounded, StringComparer.Ordinal);
            AddReferences(single, group, 0, 1);
            if (sizing.Measure(single) <= options.MaxRecordBytes) return [single];
            if (options.MaxRecords == 1) continue;

            var records = TryCreateGroup(snapshot, bounded, group, options, sizing);
            if (records != null) return records;
        }
        var fallback = Context(snapshot, sizing);
        fallback["DiagnosticFormattingFailed"] = true;
        AddReferences(fallback, group, 0, 1);
        return [fallback];
    }

    private static IReadOnlyList<Dictionary<string, object>>? TryCreateGroup(IReadOnlyDictionary<string, object> snapshot,
        Dictionary<string, object> bounded, string group, DiagnosticLogOptions options, DiagnosticJsonSize sizing)
    {
        var payload = JsonSerializer.Serialize(bounded);
        var children = new List<Dictionary<string, object>>();
        var position = 0;
        while (position < payload.Length && children.Count < options.MaxRecords - 1)
        {
            var child = Context(snapshot, sizing);
            AddReferences(child, group, children.Count + 1, options.MaxRecords);
            child["PayloadFragmentIndex"] = children.Count;
            child[PayloadFragmentCountKey] = options.MaxRecords - 1;
            child["PayloadEncoding"] = "json-text";
            if (bounded.ContainsKey(CaptureLimitedKey)) child[CaptureLimitedKey] = true;
            // Include the fragment key, colon and comma; its value is measured separately.
            var overhead = sizing.Measure(child) + sizing.MeasureString(PayloadFragmentKey) + 2;
            var length = FindFragmentLength(payload, position, overhead, options.MaxRecordBytes, sizing);
            if (length == 0) break;
            child[PayloadFragmentKey] = payload.Substring(position, length);
            children.Add(child);
            position += length;
        }
        if (position != payload.Length) return null;

        var parent = Context(snapshot, sizing);
        AddReferences(parent, group, 0, children.Count + 1);
        parent["DiagnosticSummary"] = "Diagnostic payload is in the referenced records.";
        if (bounded.ContainsKey(CaptureLimitedKey)) parent[CaptureLimitedKey] = true;
        for (var index = 0; index < children.Count; index++)
        {
            AddReferences(children[index], group, index + 1, children.Count + 1);
            children[index][PayloadFragmentCountKey] = children.Count;
        }
        return sizing.Measure(parent) <= options.MaxRecordBytes ? [parent, .. children] : null;
    }

    private static int FindFragmentLength(string payload, int position, int overhead, int maximum, DiagnosticJsonSize sizing)
    {
        var low = 0;
        var high = Math.Min(payload.Length - position, maximum);
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (overhead + sizing.MeasureString(payload.AsSpan(position, middle)) <= maximum) low = middle;
            else high = middle - 1;
        }
        if (low > 0 && position + low < payload.Length && char.IsHighSurrogate(payload[position + low - 1])) low--;
        return low;
    }

    private static Dictionary<string, object> Context(IReadOnlyDictionary<string, object> source, DiagnosticJsonSize sizing)
    {
        var context = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var key in ContextKeys)
            if (source.TryGetValue(key, out var value) &&
                (value is string or bool or int or long or Guid))
            {
                if (value is string text && (text.Length > MaxContextValueBytes || sizing.MeasureString(text) > MaxContextValueBytes))
                {
                    context[key] = DiagnosticSanitizer.Limited;
                    context[CaptureLimitedKey] = true;
                }
                else context[key] = value;
            }
        return context;
    }

    private static void AddReferences(Dictionary<string, object> record, string group, int index, int count)
    {
        record["DiagnosticGroupId"] = group;
        record["LogRecordId"] = $"{group}/{index}";
        record["ParentLogRecordId"] = $"{group}/0";
        record["PartIndex"] = index;
        record["PartCount"] = count;
        record["RelatedLogRecordIds"] = Enumerable.Range(0, count).Select(part => $"{group}/{part}").ToArray();
    }
}
