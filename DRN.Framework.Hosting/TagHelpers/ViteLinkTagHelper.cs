using DRN.Framework.Hosting.Utils.Vite;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

[HtmlTargetElement("link")]
public class ViteLinkTagHelper(IViteManifest viteManifest) : TagHelper
{
    private const string HrefAttributeName = "href";
    private const string IntegrityAttributeName = "integrity";

    public override int Order => int.MinValue; // Lower numbers execute first

    [HtmlAttributeName(HrefAttributeName)]
    public string? Href { get; set; }

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Href == null)
            return;

        if (!ViteManifest.IsViteOrigin(Href))
        {
            output.Attributes.Insert(0, new TagHelperAttribute(HrefAttributeName, Href));
            return;
        }

        var manifestItem = viteManifest.GetManifestItem(Href);
        if (manifestItem?.Path == null)
        {
            output.TagName = null;
            output.Content.SetHtmlContent($"<!-- Vite entry '{Href}' not found -->");
            return;
        }

        var path = new UrlHelper(ViewContext).Content($"~{manifestItem.Path}");
        output.Attributes.Insert(0, new TagHelperAttribute(HrefAttributeName, path));
        output.Attributes.Add(IntegrityAttributeName, manifestItem.Integrity);
    }
}
