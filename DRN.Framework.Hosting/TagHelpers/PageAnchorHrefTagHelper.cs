using System.Net;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

[HtmlTargetElement("a", Attributes = HrefAttributeName)]
public class PageAnchorHrefTagHelper : TagHelper
{
    private const string HrefAttributeName = "href";

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// Whether to apply the active CSS class when the link points to the current page.
    /// </summary>
    public bool MarkWhenActive { get; set; } = true;

    /// <summary>
    /// CSS class(es) to apply when the link is active. Default: "active fw-bold".
    /// </summary>
    public string ActiveClass { get; set; } = "active fw-bold";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!output.Attributes.TryGetAttribute(HrefAttributeName, out var hrefAttribute))
            return;

        var hrefValue = hrefAttribute.Value?.ToString();
        if (hrefAttribute.Value is IHtmlContent htmlContent)
        {
            using var writer = new StringWriter();
            htmlContent.WriteTo(writer, HtmlEncoder.Default);
            hrefValue = WebUtility.HtmlDecode(writer.ToString());
        }

        // URL resolution runs first. Only resolved root-relative URLs identify local navigation.
        if (string.IsNullOrEmpty(hrefValue) || !hrefValue.StartsWith('/') ||
            hrefValue.StartsWith("//", StringComparison.Ordinal) || hrefValue.Contains('\\'))
            return;

        if (!ViewContext.ActionDescriptor.RouteValues.TryGetValue("page", out var page) || string.IsNullOrEmpty(page))
            return;

        var suffixIndex = hrefValue.IndexOfAny(['?', '#']);
        var hrefPath = PathString.FromUriComponent(suffixIndex < 0 ? hrefValue : hrefValue[..suffixIndex]);
        var request = ViewContext.HttpContext.Request;
        var currentPath = request.PathBase.Add(request.Path);
        if (!string.Equals(hrefPath.Value?.TrimEnd('/'), currentPath.Value?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            return;

        output.Attributes.SetAttribute("aria-current", "page");
        if (!MarkWhenActive) return;

        var existingClass = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;
        output.Attributes.SetAttribute("class", $"{existingClass} {ActiveClass}".Trim());
    }
}
