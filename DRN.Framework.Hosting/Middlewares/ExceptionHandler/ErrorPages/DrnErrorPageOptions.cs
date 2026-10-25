namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

public class DrnErrorPageOptions
{
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
