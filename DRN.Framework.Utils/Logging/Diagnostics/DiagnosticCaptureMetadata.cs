namespace DRN.Framework.Utils.Logging.Diagnostics;

// ScopedLog owns this metadata under its storage lock. Mutable decisions never leave that lock.
internal readonly struct DiagnosticCaptureMetadata
{
    private readonly IReadOnlyCollection<DiagnosticNameDecision>? _decisions;
    private readonly string[]? _urlRules;

    internal bool HasValue => _decisions != null;

    internal DiagnosticCaptureMetadata(DiagnosticCaptureResult capture) : this(capture.Decisions, capture.UrlRules) { }

    internal DiagnosticCaptureMetadata(IReadOnlyCollection<DiagnosticNameDecision> decisions, string[]? urlRules)
    {
        _decisions = decisions;
        _urlRules = urlRules;
    }

    internal DiagnosticCaptureMetadata Append(DiagnosticCaptureResult capture)
    {
        if (_decisions == null) return default;
        // Only URL rules applied to every item can authorize a native copy of the list.
        var urlRules = ReferenceEquals(_urlRules, capture.UrlRules)
            ? _urlRules
            : (_urlRules ?? []).Intersect(capture.UrlRules ?? [], StringComparer.Ordinal).ToArray();
        var decisions = _decisions as List<DiagnosticNameDecision> ?? new List<DiagnosticNameDecision>(_decisions);
        decisions.AddRange(capture.Decisions);
        return new DiagnosticCaptureMetadata(decisions, urlRules);
    }

    internal DiagnosticCaptureResult Snapshot(object value) => new(value,
        _decisions as DiagnosticNameDecision[] ??
        (_decisions ?? throw new InvalidOperationException("No diagnostic capture metadata is available.")).ToArray(), _urlRules);
}
