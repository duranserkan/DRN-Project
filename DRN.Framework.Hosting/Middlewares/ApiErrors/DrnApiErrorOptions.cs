namespace DRN.Framework.Hosting.Middlewares.ApiErrors;

/// <summary>Identifies API requests for unmatched routes and routing-generated method rejections.</summary>
public sealed class DrnApiErrorOptions
{
    /// <summary>
    /// Application-relative path prefixes, such as /Api. Empty by default.
    /// Matched endpoints use metadata instead. Prefix matching respects path segments.
    /// </summary>
    public string[] PathPrefixes { get; set; } = [];

    internal static bool IsValidPrefix(string? prefix) =>
        !string.IsNullOrWhiteSpace(prefix) && prefix.StartsWith('/') && prefix.Length > 1 &&
        !prefix.Contains('\\') && !prefix.Contains('?') && !prefix.Contains('#') &&
        prefix[1..].Split('/').All(segment =>
            !string.IsNullOrWhiteSpace(segment) && segment is not "." and not "..");
}
