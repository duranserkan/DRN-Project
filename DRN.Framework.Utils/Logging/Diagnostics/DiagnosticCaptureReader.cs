using System.Collections;
using static DRN.Framework.Utils.Logging.Diagnostics.DiagnosticSanitizer;

namespace DRN.Framework.Utils.Logging.Diagnostics;

// Reads detached captures only. The owner supplies the guard, budget, and destination policy.
internal sealed class DiagnosticCaptureReader(DiagnosticSanitizer owner)
{
    private DiagnosticLogOptions? _firstSourceOptions;
    private DiagnosticSanitizer? _firstSource;
    // Additional source policies only. Visited nodes bound its size; Materialize bounds its lifetime.
    private Dictionary<DiagnosticLogOptions, DiagnosticSanitizer>? _sources;

    internal object? Copy(object? value, int depth)
    {
        if (!owner.VisitNode(depth)) return Limited;
        if (value is DiagnosticCapturedValue captured)
            return captured with { Value = Copy(captured.Value, depth) };
        if (value is string text) return owner.Accept(text) ? text : Limited;
        if (value is IDictionary<string, object?> map) return CopyMap(map, depth);
        if (value is IList list) return CopyList(list, depth);
        return value;
    }

    private Dictionary<string, object?> CopyMap(IDictionary<string, object?> map, int depth)
    {
        var result = new Dictionary<string, object?>();
        foreach (var pair in map)
        {
            if (result.Count >= owner.Options.MaxCollectionEntries || !owner.WorkAvailable()) { result[CollectionLimitKey] = Limited; break; }
            result[owner.UniqueKey(result, pair.Key)] = Copy(pair.Value, depth + 1);
        }
        return result;
    }

    private List<object?> CopyList(IList list, int depth)
    {
        var result = new List<object?>();
        foreach (var item in list)
        {
            if (result.Count >= owner.Options.MaxCollectionEntries || !owner.WorkAvailable()) { result.Add(Limited); break; }
            result.Add(Copy(item, depth + 1));
        }
        return result;
    }

    internal IReadOnlyDictionary<string, object> Materialize(IReadOnlyDictionary<string, object> snapshot, bool redact)
    {
        try
        {
            var output = new Dictionary<string, object>();
            foreach (var pair in snapshot)
            {
                if (output.Count >= owner.Options.MaxNodes || !owner.WorkAvailable()) { output[CollectionLimitKey] = Limited; break; }
                output[pair.Key] = Read(pair.Value, 0, redact)!;
            }
            return output;
        }
        finally
        {
            _firstSourceOptions = null;
            _firstSource = null;
            _sources = null;
        }
    }

    private object? Read(object? value, int depth, bool redact)
    {
        if (!owner.VisitNode(depth)) return Limited;
        if (value is DiagnosticCapturedValue captured) return ReadValue(captured, depth, redact);
        if (value is string scalar) return owner.Accept(scalar) ? scalar : Limited;
        if (value is IReadOnlyDictionary<string, object?> map) return ReadMap(map, depth, redact);
        if (value is IList list) return ReadList(list, depth, redact);
        return value;
    }

    private object? ReadValue(DiagnosticCapturedValue captured, int depth, bool redact)
    {
        if (redact && captured.Kind == DiagnosticRedactionKind.Classified) return Redacted;
        if (redact && captured.Value is string text)
        {
            if (!owner.Accept(text)) return Limited;
            if (captured.SourceOptions is { } sourceOptions)
                text = SourceWorker(sourceOptions).RedactCapturedUrlWithFreshBudget(text);
            return owner.RedactCapturedUrl(text);
        }
        return Read(captured.Value, depth, redact);
    }

    private DiagnosticSanitizer SourceWorker(DiagnosticLogOptions options)
    {
        if (_firstSource == null)
        {
            _firstSource = new DiagnosticSanitizer(options);
            _firstSourceOptions = options;
            return _firstSource;
        }
        if (ReferenceEquals(options, _firstSourceOptions)) return _firstSource;
        _sources ??= new(ReferenceEqualityComparer.Instance);
        if (!_sources.TryGetValue(options, out var source))
        {
            source = new DiagnosticSanitizer(options);
            _sources.Add(options, source);
        }
        return source;
    }

    private Dictionary<string, object?> ReadMap(IReadOnlyDictionary<string, object?> map, int depth, bool redact)
    {
        var output = new Dictionary<string, object?>();
        foreach (var pair in map)
        {
            if (output.Count > owner.Options.MaxCollectionEntries || !owner.WorkAvailable()) { output[CollectionLimitKey] = Limited; break; }
            output[pair.Key] = Read(pair.Value, depth + 1, redact);
        }
        return output;
    }

    private List<object?> ReadList(IList list, int depth, bool redact)
    {
        var output = new List<object?>();
        foreach (var item in list)
        {
            if (output.Count > owner.Options.MaxCollectionEntries || !owner.WorkAvailable()) { output.Add(Limited); break; }
            output.Add(Read(item, depth + 1, redact));
        }
        return output;
    }
}
