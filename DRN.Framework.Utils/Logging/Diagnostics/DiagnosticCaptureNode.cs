namespace DRN.Framework.Utils.Logging.Diagnostics;

// Only path length is needed for capture bounds now that names are compiler-owned.
internal readonly record struct DiagnosticCaptureNode(int PathLength, bool Classified = false)
{
    internal DiagnosticCaptureNode(string path, bool classified = false) : this(path.Length, classified) { }
}
