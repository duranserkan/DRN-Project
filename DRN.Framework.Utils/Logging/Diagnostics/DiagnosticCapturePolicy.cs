namespace DRN.Framework.Utils.Logging.Diagnostics;

/// <summary>The structural node to which an exact diagnostic name exception applies.</summary>
public enum DiagnosticCaptureKind { NamedEntry, ObjectRoot, Property, DictionaryKey, JsonProperty, ListItem }

/// <summary>An application-authored exception for one node, never its descendants.</summary>
public sealed class DiagnosticNameException
{
    public DiagnosticNameException(DiagnosticCaptureKind kind, string justification, params string[] segments)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Length == 0 || segments.Any(segment => segment == null))
            throw new ArgumentException("Specify original path segments.", nameof(segments));
        Kind = kind;
        Justification = justification;
        Segments = Array.AsReadOnly((string[])segments.Clone());
    }

    public DiagnosticCaptureKind Kind { get; }
    public string Justification { get; }
    public IReadOnlyList<string> Segments { get; }
}

/// <summary>Immutable, explicit capture exceptions. Never construct this policy from request data.</summary>
public sealed class DiagnosticCapturePolicy
{
    private readonly DiagnosticNameException[] _exceptions;
    public DiagnosticCapturePolicy(params DiagnosticNameException[] exceptions)
    {
        ArgumentNullException.ThrowIfNull(exceptions);
        if (exceptions.Any(item => item == null)) throw new ArgumentException("Exceptions cannot be null.", nameof(exceptions));
        _exceptions = (DiagnosticNameException[])exceptions.Clone();
    }

    internal void Validate(DiagnosticLogOptions options)
    {
        if (_exceptions.Length > options.MaxNodes || _exceptions.Any(item =>
                item.Segments.Count > options.MaxDepth + 1 || item.Segments.Any(segment =>
                    segment.Length > options.MaxKeyBytes || segment.Any(char.IsControl)) ||
                string.Join('.', item.Segments).Length > options.MaxKeyBytes || item.Justification.Length > options.MaxFieldBytes))
            throw new ArgumentException("Diagnostic exceptions exceed capture limits.");
    }

    internal bool Exempts(DiagnosticCaptureKind kind, IReadOnlyList<string> segments) =>
        _exceptions.Any(item => item.Kind == kind && item.Segments.SequenceEqual(segments, StringComparer.Ordinal));
}

internal sealed record DiagnosticNameDecision(string Path, bool Exempt, string? NamePath = null);
internal sealed record DiagnosticCaptureResult(object? Value, DiagnosticNameDecision[] Decisions, string[]? UrlRules = null);

internal sealed class DiagnosticCaptureContext(DiagnosticCapturePolicy? policy)
{
    internal List<DiagnosticNameDecision> Decisions { get; } = [];
    internal bool Exempts(DiagnosticCaptureKind kind, IReadOnlyList<string> segments) => policy?.Exempts(kind, segments) == true;
}
