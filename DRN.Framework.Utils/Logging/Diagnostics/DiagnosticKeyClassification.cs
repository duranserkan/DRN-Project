using DRN.Framework.SharedKernel.Attributes;

namespace DRN.Framework.Utils.Logging.Diagnostics;

public enum DiagnosticDataKind
{
    Sensitive = 1,
    Personal = 2,
    NonSensitive = 3
}

/// <summary>Application-authored classification of one exact dictionary key, never its descendants.</summary>
public sealed record DiagnosticKeyClassification(
    [property: Sensitive(false, Justification = "Dictionary field name, not its value.")] string Key,
    DiagnosticDataKind Kind,
    string? Reason = null);
