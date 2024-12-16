using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using NetEscapades.AspNetCore.SecurityHeaders;

namespace DRN.Framework.Hosting.TagHelpers;

[HtmlTargetElement("script")]
[HtmlTargetElement("iframe")]
public class NonceTagHelper : TagHelper
{
    [ViewContext] public ViewContext? ViewContext { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(ViewContext);
        output.Attributes.Add("nonce", ViewContext.HttpContext.GetNonce());
    }
}