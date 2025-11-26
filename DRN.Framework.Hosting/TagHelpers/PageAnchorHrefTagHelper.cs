using Flurl;
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
        if (!context.AllAttributes.TryGetAttribute(HrefAttributeName, out var hrefAttribute))
            return;

        var hrefValue = hrefAttribute.Value?.ToString();
        if (string.IsNullOrEmpty(hrefValue))
            return;

        var hrefPage = new Url(hrefValue).Path.Trim('/');
        var currentRoutePage = ViewContext.ActionDescriptor.RouteValues["page"]?.Trim('/') ?? string.Empty;
        if (!hrefPage.Equals(currentRoutePage, StringComparison.OrdinalIgnoreCase))
            return;

        output.Attributes.SetAttribute("aria-current", "page");
        if (!MarkWhenActive) return;

        var existingClass = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;
        output.Attributes.SetAttribute("class", $"{existingClass} {ActiveClass}".Trim());
    }
}