using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

/// <summary>Resolves explicit ~/ application URLs, including HTMX request attributes.</summary>
[HtmlTargetElement(Attributes = "href")]
[HtmlTargetElement(Attributes = "src")]
[HtmlTargetElement(Attributes = "action")]
[HtmlTargetElement(Attributes = "formaction")]
[HtmlTargetElement(Attributes = "hx-get")]
[HtmlTargetElement(Attributes = "hx-post")]
[HtmlTargetElement(Attributes = "hx-put")]
[HtmlTargetElement(Attributes = "hx-patch")]
[HtmlTargetElement(Attributes = "hx-delete")]
public class ApplicationUrlTagHelper(IUrlHelperFactory urlHelperFactory, HtmlEncoder htmlEncoder)
    : UrlResolutionTagHelper(urlHelperFactory, htmlEncoder)
{
    private static readonly string[] HtmxAttributes = ["hx-get", "hx-post", "hx-put", "hx-patch", "hx-delete"];

    // After Vite source resolution, before MVC's built-in URL resolution.
    public override int Order => int.MinValue + 100;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        base.Process(context, output);
        if (output.TagName == null) return;

        foreach (var name in HtmxAttributes)
            ProcessUrlAttribute(name, output);
    }
}
