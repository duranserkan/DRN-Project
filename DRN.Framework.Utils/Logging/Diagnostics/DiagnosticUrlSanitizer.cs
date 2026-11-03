using static DRN.Framework.Utils.Logging.Diagnostics.DiagnosticSanitizer;

namespace DRN.Framework.Utils.Logging.Diagnostics;

// Structural URL and form processing shares capture budgets without classifying names.
internal sealed class DiagnosticUrlSanitizer(DiagnosticSanitizer owner)
{
    internal string Url(string value, bool countNodes = false)
    {
        if (!owner.WorkAvailable()) return Limited;
        if (value.Length == 0) return value;
        var relative = value.StartsWith('/') || value.StartsWith('?') || value.StartsWith('#');
        if (!Uri.TryCreate(AbsoluteUrl(value, relative),
                UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "ftp" or "file" or "otpauth")) return Invalid;
        var path = UrlPath(uri);
        if (path == Limited) return Limited;
        var query = uri.Query.Length > 1 ? "?" + Pairs(uri.Query[1..], countNodes) : "";
        var fragment = UrlFragment(uri.Fragment, countNodes);
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
        for (var index = 0; index < segments.Length; index++)
        {
            var decoded = Decode(segments[index]);
            if (!owner.WorkAvailable()) return Limited;
            segments[index] = Uri.EscapeDataString(decoded);
        }
        return string.Join('/', segments);
    }

    private string UrlFragment(string fragment, bool countNodes)
    {
        if (fragment.Length <= 1) return "";
        return fragment.Contains('=') ? "#" + Pairs(fragment[1..], countNodes) : fragment;
    }

    internal string Pairs(string value, bool countNodes = false)
    {
        if (!owner.WorkAvailable()) return Limited;
        var result = new List<string>();
        foreach (var part in value.Split('&', owner.Options.MaxCollectionEntries + 1))
        {
            if (result.Count == owner.Options.MaxCollectionEntries || !owner.WorkAvailable()) { result.Add(QueryLimitMarker); break; }
            if (countNodes && !owner.CountNode()) { result.Add(QueryLimitMarker); break; }
            result.Add(Pair(part));
        }
        return string.Join('&', result);
    }

    private string Pair(string part)
    {
        var separator = part.IndexOf('=');
        var key = Decode((separator < 0 ? part : part[..separator]).Replace('+', ' '));
        var text = separator < 0 ? "" : Decode(part[(separator + 1)..].Replace('+', ' '));
        var safeKey = owner.SanitizeKey(key);
        return Uri.EscapeDataString(safeKey) + "=" + Uri.EscapeDataString(text);
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
