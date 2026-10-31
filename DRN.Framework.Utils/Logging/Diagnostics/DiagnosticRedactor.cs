using Microsoft.Extensions.Compliance.Redaction;

namespace DRN.Framework.Utils.Logging.Diagnostics;

/// <summary>Replaces classified text with the diagnostic marker without retaining its content.</summary>
public sealed class DiagnosticRedactor : Redactor
{
    public override int GetRedactedLength(ReadOnlySpan<char> input) => DiagnosticSanitizer.Redacted.Length;

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        DiagnosticSanitizer.Redacted.AsSpan().CopyTo(destination);
        return DiagnosticSanitizer.Redacted.Length;
    }
}
