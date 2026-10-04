using DRN.Framework.Hosting.Utils.Vite;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

[HtmlTargetElement("script")]
public class ViteScriptTagHelper(IViteManifest viteManifest) : TagHelper
{
    private const string SrcAttributeName = "src";
    private const string IntegrityAttributeName = "integrity";

    public override int Order => int.MinValue; // Lower numbers execute first

    [HtmlAttributeName(SrcAttributeName)]
    public string? Src { get; set; }

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Src == null)
            return;

        if (!ViteManifest.IsViteOrigin(Src))
        {
            output.Attributes.Insert(0, new TagHelperAttribute(SrcAttributeName, Src));
            return;
        }

        var manifestItem = viteManifest.GetManifestItem(Src);
        if (manifestItem?.Path == null)
        {
            output.TagName = null;
            output.Content.SetHtmlContent($"<!-- Vite entry '{Src}' not found -->");
            return;
        }

        var path = new UrlHelper(ViewContext).Content($"~{manifestItem.Path}");
        output.Attributes.Insert(0, new TagHelperAttribute(SrcAttributeName, path));
        output.Attributes.Add(IntegrityAttributeName, manifestItem.Integrity);
    }
}
