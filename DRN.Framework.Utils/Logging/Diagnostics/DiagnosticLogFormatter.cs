using System.Text.Json;

namespace DRN.Framework.Utils.Logging.Diagnostics;

internal static class DiagnosticLogFormatter
{
    private static readonly string[] ContextKeys =
    ["CorrelationId", "TraceId", "TraceIdentifier", "ClientError.ClientErrorId", "EventId", "EventName", "EventOutcome", "ExceptionType", "Response_StatusCode", "HttpMethod"];

    internal static Dictionary<string, object> BoundSnapshot(IReadOnlyDictionary<string, object> snapshot, int maximum)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        var bytes = 2;
        var limited = false;
        foreach (var key in ContextKeys.Concat(snapshot.Keys).Distinct(StringComparer.Ordinal))
        {
            if (!snapshot.TryGetValue(key, out var value)) continue;
            var size = JsonSerializer.SerializeToUtf8Bytes(key).Length + JsonSerializer.SerializeToUtf8Bytes(value).Length + 2;
            if (bytes + size > maximum - 64) { limited = true; continue; }
            result[key] = value;
            bytes += size;
        }
        if (limited) result["DiagnosticCaptureLimited"] = true;
        return result;
    }

    internal static IReadOnlyList<Dictionary<string, object>> Create(IReadOnlyDictionary<string, object> snapshot, DiagnosticLogOptions options)
    {
        var group = Guid.NewGuid().ToString("N");
        var budget = options.MaxScopeBytes;
        for (var attempt = 0; attempt < 12; attempt++, budget /= 2)
        {
            var bounded = BoundSnapshot(snapshot, budget);
            if (attempt > 0) bounded["DiagnosticCaptureLimited"] = true;
            var single = new Dictionary<string, object>(bounded, StringComparer.Ordinal);
            AddReferences(single, group, 0, 1);
            if (Size(single) <= options.MaxRecordBytes) return [single];
            if (options.MaxRecords == 1) continue;

            var payload = JsonSerializer.Serialize(bounded);
            var children = new List<Dictionary<string, object>>();
            var position = 0;
            while (position < payload.Length && children.Count < options.MaxRecords - 1)
            {
                var child = Context(snapshot);
                AddReferences(child, group, children.Count + 1, options.MaxRecords);
                child["PayloadFragmentIndex"] = children.Count;
                child["PayloadFragmentCount"] = options.MaxRecords - 1;
                child["PayloadEncoding"] = "json-text";
                if (bounded.ContainsKey("DiagnosticCaptureLimited")) child["DiagnosticCaptureLimited"] = true;
                var low = 0;
                var high = Math.Min(payload.Length - position, options.MaxRecordBytes);
                while (low < high)
                {
                    var middle = (low + high + 1) / 2;
                    child["PayloadFragment"] = payload.Substring(position, middle);
                    if (Size(child) <= options.MaxRecordBytes) low = middle;
                    else high = middle - 1;
                }
                if (low == 0) break;
                if (position + low < payload.Length && char.IsHighSurrogate(payload[position + low - 1])) low--;
                if (low == 0) break;
                child["PayloadFragment"] = payload.Substring(position, low);
                children.Add(child);
                position += low;
            }
            if (position != payload.Length) continue;
            var parent = Context(snapshot);
            AddReferences(parent, group, 0, children.Count + 1);
            parent["DiagnosticSummary"] = "Diagnostic payload is in the referenced records.";
            if (bounded.ContainsKey("DiagnosticCaptureLimited")) parent["DiagnosticCaptureLimited"] = true;
            for (var index = 0; index < children.Count; index++)
            {
                AddReferences(children[index], group, index + 1, children.Count + 1);
                children[index]["PayloadFragmentCount"] = children.Count;
            }
            if (Size(parent) <= options.MaxRecordBytes) return [parent, .. children];
        }
        var fallback = Context(snapshot);
        fallback["DiagnosticFormattingFailed"] = true;
        AddReferences(fallback, group, 0, 1);
        return [fallback];
    }

    private static Dictionary<string, object> Context(IReadOnlyDictionary<string, object> source)
    {
        var context = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var key in ContextKeys)
            if (source.TryGetValue(key, out var value) &&
                (value is string or bool or int or long or Guid))
            {
                if (value is string text && (text.Length > 256 || JsonSerializer.SerializeToUtf8Bytes(text).Length > 256))
                {
                    context[key] = DiagnosticSanitizer.Limited;
                    context["DiagnosticCaptureLimited"] = true;
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

    private static int Size(Dictionary<string, object> record) => JsonSerializer.SerializeToUtf8Bytes(record).Length;
}
