using DRN.Framework.Hosting.Utils;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

[HtmlTargetElement("link")]
public class ViteLinkTagHelper : TagHelper
{
    private const string HrefAttributeName = "href";

    public override int Order => int.MinValue; // Lower numbers execute first

    [HtmlAttributeName(HrefAttributeName)]
    public string? Href { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Href == null)
            return;

        if (!ViteManifest.IsViteOrigin(Href))
        {
            output.Attributes.Insert(0, new TagHelperAttribute(HrefAttributeName, Href));
            return;
        }

        var hrefPath = ViteManifest.GetPath(Href);
        if (hrefPath == null)
        {
            output.TagName = null;
            output.Content.SetHtmlContent($"<!-- Vite entry '{Href}' not found -->");
            return;
        }

        output.Attributes.Insert(0, new TagHelperAttribute(HrefAttributeName, hrefPath));
    }
}