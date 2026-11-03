namespace DRN.Framework.Utils.Logging.Diagnostics;

// Name matching and exemptions describe one structural node, independently of traversal state.
internal readonly record struct DiagnosticCaptureNode(string Path, string[] Segments,
    DiagnosticCaptureKind Kind = DiagnosticCaptureKind.NamedEntry, bool Exempt = false,
    string? NamePath = null, bool Classified = false);
