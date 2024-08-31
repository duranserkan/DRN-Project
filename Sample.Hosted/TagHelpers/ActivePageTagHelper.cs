using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Sample.Hosted.TagHelpers;

[HtmlTargetElement("a", Attributes = "active-page")]
public class ActivePageTagHelper() : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; }

    public string ActivePage { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var currentRoutePage = ViewContext.ActionDescriptor.RouteValues["page"];

        if (!string.Equals(ActivePage, currentRoutePage, StringComparison.OrdinalIgnoreCase)) return;

        var existingClass = output.Attributes["class"]?.Value?.ToString()??string.Empty;
        output.Attributes.SetAttribute("class", $"{existingClass} active".Trim());
    }
}