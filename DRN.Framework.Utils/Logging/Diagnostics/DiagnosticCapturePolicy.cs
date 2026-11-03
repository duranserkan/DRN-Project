namespace DRN.Framework.Utils.Logging.Diagnostics;

/// <summary>The structural kind recorded in a validated diagnostic capture policy.</summary>
public enum DiagnosticCaptureKind
{
    NamedEntry = 1,
    ObjectRoot = 2,
    Property = 3,
    DictionaryKey = 4,
    ListItem = 5
}

/// <summary>An application-authored review entry. Runtime capture validates entries without matching names.</summary>
public sealed class DiagnosticNameExemption
{
    public DiagnosticNameExemption(DiagnosticCaptureKind kind, string justification, params string[] segments)
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

/// <summary>Immutable capture review metadata retained for compatibility. Never construct it from request data.</summary>
public sealed class DiagnosticCapturePolicy
{
    private readonly DiagnosticNameExemption[] _exceptions;
    public DiagnosticCapturePolicy(params DiagnosticNameExemption[] exceptions)
    {
        ArgumentNullException.ThrowIfNull(exceptions);
        if (exceptions.Any(item => item == null)) throw new ArgumentException("Exceptions cannot be null.", nameof(exceptions));
        _exceptions = (DiagnosticNameExemption[])exceptions.Clone();
    }

    internal void Validate(DiagnosticLogOptions options)
    {
        if (_exceptions.Length > options.MaxNodes || _exceptions.Any(item =>
                item.Segments.Count > options.MaxDepth + 1 || item.Segments.Any(segment =>
                    segment.Length > options.MaxKeyBytes || segment.Any(char.IsControl)) ||
                string.Join('.', item.Segments).Length > options.MaxKeyBytes || item.Justification.Length > options.MaxFieldBytes))
            throw new ArgumentException("Diagnostic exceptions exceed capture limits.");
    }
}

internal sealed record DiagnosticCaptureResult(object? Value);

// Metadata wraps detached values, never caller-owned objects. GetLogs removes these wrappers.
internal enum DiagnosticRedactionKind { Classified, Url }
internal sealed record DiagnosticCapturedValue(object? Value, DiagnosticRedactionKind Kind,
    [property: System.Text.Json.Serialization.JsonIgnore] DiagnosticLogOptions? SourceOptions = null);

internal readonly struct DiagnosticCaptureContext(bool deferRedaction = false)
{
    internal bool DeferRedaction { get; } = deferRedaction;
}
