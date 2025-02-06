using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

[HtmlTargetElement("a", Attributes = "href")]
public class PageAnchorTagHelper : TagHelper
{
    [ViewContext] [HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = null!;

    public bool MarkWhenActive { get; set; } = true;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!output.Attributes.TryGetAttribute("href", out var hrefAttribute))
            return;

        var hrefValue = hrefAttribute.Value?.ToString();
        if (string.IsNullOrEmpty(hrefValue))
            return;

        // output.Attributes.SetAttribute("hx-get", hrefValue);
        //
        // if (!output.Attributes.TryGetAttribute("hx-target", out _)) //todo: make hx-target default customizable
        //     output.Attributes.SetAttribute("hx-target", "#bodyContent");
        //
        // if (!output.Attributes.TryGetAttribute("hx-push-url", out _)) //todo: make hx-target default customizable
        //     output.Attributes.SetAttribute("hx-push-url", "true");

        var currentRoutePage = ViewContext.ActionDescriptor.RouteValues["page"];
        if (!string.Equals(hrefValue, currentRoutePage, StringComparison.OrdinalIgnoreCase))
            return;

        output.Attributes.SetAttribute("aria-current", "page");
        if (MarkWhenActive) //todo make active style customizable
        {
            var existingClass = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;
            output.Attributes.SetAttribute("class", $"{existingClass} active fw-bold".Trim());
        }
    }
}