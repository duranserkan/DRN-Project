// This file is licensed to you under the MIT license.
// Derived from https://github.com/andrewlock/NetEscapades.AspNetCore.SecurityHeaders/blob/main/src/NetEscapades.AspNetCore.SecurityHeaders.TagHelpers/NonceTagHelper.cs

using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using NetEscapades.AspNetCore.SecurityHeaders;

namespace DRN.Framework.Hosting.TagHelpers;

[HtmlTargetElement("iframe")]
[HtmlTargetElement("script")]
[HtmlTargetElement("style")]
[HtmlTargetElement("link")]
public class NonceTagHelper : TagHelper
{
    [ViewContext] public ViewContext? ViewContext { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(ViewContext);
        var disableNonce = output.Attributes.ContainsName("disable-nonce");

        if (disableNonce)
        {
            output.Attributes.Add("nonce-generation", "disabled");
            output.Attributes.RemoveAll("disable-nonce");
        }

        else
            output.Attributes.Add("Nonce", ViewContext.HttpContext.GetNonce());
    }
}