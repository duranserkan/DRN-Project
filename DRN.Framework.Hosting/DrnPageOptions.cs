using DRN.Framework.SharedKernel;

namespace DRN.Framework.Hosting;

public class DrnPageOptions
{
    /// <summary>Host-provided application-root-relative browser-tab icon. Null preserves each page's default.</summary>
    public string? FaviconUrl { get; set; }

    internal static bool IsValidFaviconUrl(string? url) => url is null ||
        (url.StartsWith('/') && url.Length > 1 &&
         url.Split('/').Skip(1).All(segment => segment.Length > 0 && segment is not "." and not ".." &&
             segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')));

    internal string GetFaviconHeadContent(string routePrefix)
    {
        if (!IsValidFaviconUrl(FaviconUrl))
            throw new ConfigurationException("FaviconUrl must be an application-root-relative asset path, such as /images/icon.svg.");
        if (FaviconUrl is null) return string.Empty;
        var parents = string.Concat(Enumerable.Repeat("../", routePrefix.Split('/').Length));
        return $"<link rel=\"icon\" href=\"{parents}{FaviconUrl.TrimStart('/')}\">";
    }

    /// <summary>
    /// Razor page name under Pages, without the .cshtml extension.
    /// The page must use BadRequestPageModel. Null disables custom bad-request pages.
    /// </summary>
    public string? BadRequestPage { get; set; }

    /// <summary>
    /// Razor page name under Pages, without the .cshtml extension.
    /// The page must use ServerErrorPageModel. Null disables custom server-error pages.
    /// </summary>
    public string? ServerErrorPage { get; set; }

    /// <summary>
    /// Razor page name under Pages, without the .cshtml extension.
    /// The page must use NotFoundPageModel. Null disables custom not-found pages.
    /// </summary>
    public string? NotFoundPage { get; set; }

    internal static bool IsValidPageName(string? page) => page == null ||
        (page.StartsWith('/') && page.Length > 1 &&
         !page.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) &&
         !page.Contains('\\') && !page.Contains('?') && !page.Contains('#') &&
         page[1..].Split('/').All(segment =>
             !string.IsNullOrWhiteSpace(segment) && segment is not "." and not ".."));
}
