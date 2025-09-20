using DRN.Framework.Hosting.Utils;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

[HtmlTargetElement("script")]
public class ViteScriptTagHelper : TagHelper
{
    private const string SrcAttributeName = "src";

    public override int Order => int.MinValue; // Lower numbers execute first

    [HtmlAttributeName(SrcAttributeName)]
    public string? Src { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Src == null)
            return;

        if (!ViteManifest.IsViteOrigin(Src))
        {
            output.Attributes.Insert(0, new TagHelperAttribute(SrcAttributeName, Src));
            return;
        }

        var scriptPath = ViteManifest.GetPath(Src);
        if (scriptPath == null)
        {
            output.TagName = null;
            output.Content.SetHtmlContent($"<!-- Vite entry '{Src}' not found -->");
            return;
        }

        output.Attributes.Insert(0, new TagHelperAttribute(SrcAttributeName, scriptPath));
    }
}