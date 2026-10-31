namespace DRN.Framework.Hosting.Middlewares.ApiErrors;

/// <summary>Configures API route detection and optional HTMX error formatting.</summary>
public sealed class DrnApiErrorOptions
{
    /// <summary>
    /// Application-relative path prefixes, such as /Api. Empty by default.
    /// Matched endpoints use metadata instead. Prefix matching respects path segments.
    /// </summary>
    public string[] PathPrefixes { get; set; } = [];

    /// <summary>
    /// Formats errors for requests with exactly one HX-Request: true header using Problem Details.
    /// Disabled by default. Does not change API classification or authentication behavior.
    /// Explicit non-problem response bodies and DisableDrnProblemDetails remain respected.
    /// </summary>
    public bool HandleHtmxRequests { get; set; }

    internal static bool IsValidPrefix(string? prefix) =>
        !string.IsNullOrWhiteSpace(prefix) && prefix.StartsWith('/') && prefix.Length > 1 &&
        !prefix.Contains('\\') && !prefix.Contains('?') && !prefix.Contains('#') &&
        prefix[1..].Split('/').All(segment =>
            !string.IsNullOrWhiteSpace(segment) && segment is not "." and not "..");
}
