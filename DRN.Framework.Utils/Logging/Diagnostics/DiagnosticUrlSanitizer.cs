using static DRN.Framework.Utils.Logging.Diagnostics.DiagnosticSanitizer;

namespace DRN.Framework.Utils.Logging.Diagnostics;

// Structural URL and form processing shares the owner's guarded capture budget and name rules.
internal sealed class DiagnosticUrlSanitizer(DiagnosticSanitizer owner, DiagnosticNameRules rules)
{
    internal const string UrlRedacted = "redacted";

    internal string Url(string value, DiagnosticCaptureContext? context = null)
    {
        if (!owner.WorkAvailable()) return Limited;
        if (value.Length == 0) return value;
        var relative = value.StartsWith('/') || value.StartsWith('?') || value.StartsWith('#');
        if (!Uri.TryCreate(AbsoluteUrl(value, relative),
                UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "ftp" or "file" or "otpauth")) return Invalid;
        var path = UrlPath(uri);
        if (path == Limited) return Limited;
        var query = uri.Query.Length > 1 ? "?" + Pairs(uri.Query[1..], context, UrlRedacted) : "";
        var fragment = UrlFragment(uri.Fragment, context);
        var components = owner.RedactionEnabled
            ? UriComponents.SchemeAndServer : UriComponents.SchemeAndServer | UriComponents.UserInfo;
        var authority = relative ? "" : uri.GetComponents(components, UriFormat.UriEscaped);
        return authority + path + query + fragment;
    }

    private static string AbsoluteUrl(string value, bool relative)
    {
        if (!relative) return value;
        var separator = value.StartsWith('/') ? "" : "/";
        return "https://diagnostic.invalid" + separator + value;
    }

    private string UrlPath(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/');
        var redactNext = false;
        for (var index = 0; index < segments.Length; index++)
        {
            var decoded = Decode(segments[index]);
            if (!owner.WorkAvailable()) return Limited;
            segments[index] = Uri.EscapeDataString(redactNext ? UrlRedacted : decoded);
            redactNext = owner.RedactionEnabled && rules.MatchesUrlSegment(decoded);
        }
        return string.Join('/', segments);
    }

    private string UrlFragment(string fragment, DiagnosticCaptureContext? context)
    {
        if (fragment.Length <= 1) return "";
        return fragment.Contains('=') ? "#" + Pairs(fragment[1..], context, UrlRedacted) : fragment;
    }

    internal string Pairs(string value, DiagnosticCaptureContext? context = null, string redacted = Redacted,
        string sourcePath = "")
    {
        if (!owner.WorkAvailable()) return Limited;
        var result = new List<string>();
        foreach (var part in value.Split('&', owner.Options.MaxCollectionEntries + 1))
        {
            if (result.Count == owner.Options.MaxCollectionEntries || !owner.WorkAvailable()) { result.Add(QueryLimitMarker); break; }
            if (context != null && !owner.CountNode()) { result.Add(QueryLimitMarker); break; }
            result.Add(Pair(part, context, redacted, sourcePath));
        }
        return string.Join('&', result);
    }

    private string Pair(string part, DiagnosticCaptureContext? context, string redacted, string sourcePath)
    {
        var separator = part.IndexOf('=');
        var key = Decode((separator < 0 ? part : part[..separator]).Replace('+', ' '));
        var text = separator < 0 ? "" : Decode(part[(separator + 1)..].Replace('+', ' '));
        var safeKey = owner.SanitizeKey(key);
        var fieldPath = sourcePath.Length == 0 ? key : sourcePath + "." + key;
        var sensitive = owner.IsSensitive(fieldPath);
        var safeValue = sensitive ? redacted : text;
        // Query and fragment keys have independent name rules, even on an exempt URL property.
        if (context != null && !sensitive) context.Decisions.Add(new(fieldPath, false));
        return Uri.EscapeDataString(safeKey) + "=" + Uri.EscapeDataString(safeValue);
    }

    private static string Decode(string value)
    {
        for (var round = 0; round < 3 && value.Contains('%'); round++)
        {
            for (var index = 0; index < value.Length; index++)
                if (value[index] == '%' && (index + 2 >= value.Length || !Uri.IsHexDigit(value[index + 1]) || !Uri.IsHexDigit(value[index + 2])))
                    throw new FormatException("Invalid diagnostic URL encoding.");
            value = Uri.UnescapeDataString(value);
        }
        return value.Contains('%') ? Invalid : value;
    }
}
