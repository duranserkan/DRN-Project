using System.Text.Json;

namespace DRN.Framework.SharedKernel.Diagnostics;

// Linked into the analyzer. Both assemblies embed the same source-owned JSON catalog.
internal static class DiagnosticRuleCatalog
{
    internal static readonly string[] SecretFields = Read("secretFields");
    internal static readonly string[] PersonalFields = Read("personalFields");
    internal static readonly string[] FieldPaths = Read("sensitiveFieldPaths");
    internal static readonly string[] Fields = [.. SecretFields, .. PersonalFields, .. FieldPaths];
    internal static readonly string[] UrlSegments = Read("secretPathSegments");

    internal static string Normalize(string value) => string.Concat(value.Where(char.IsAsciiLetterOrDigit)).ToLowerInvariant();

    internal static ReadOnlySpan<char> Normalize(ReadOnlySpan<char> value, Span<char> buffer)
    {
        var length = 0;
        foreach (var character in value)
            if (char.IsAsciiLetterOrDigit(character)) buffer[length++] = char.ToLowerInvariant(character);
        return buffer[..length];
    }

    internal static bool Matches(ReadOnlySpan<char> normalized, IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
            if (normalized.Contains(pattern, StringComparison.Ordinal)) return true;
        return false;
    }

    internal static bool MatchesField(string path) => Matches(Normalize(path), Fields);

    private static string[] Read(params string[] groups)
    {
        using var stream = typeof(DiagnosticRuleCatalog).Assembly.GetManifestResourceStream("DRN.DiagnosticRules.json")
                           ?? throw new InvalidOperationException("Diagnostic rule catalog is missing.");
        using var document = JsonDocument.Parse(stream);
        return groups.SelectMany(group => document.RootElement.GetProperty(group).EnumerateArray())
            .Select(value => Normalize(value.GetString()!)).Where(value => value.Length != 0).ToArray();
    }
}
