using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Sample.Hosted.TagHelpers;

[HtmlTargetElement("*", Attributes = "authorized-only")]
public class AuthorizedOnlyTagHelper : TagHelper
{
    /// <summary>
    /// Indicates that the link should only be rendered if the user meets certain security conditions.
    /// </summary>
    [HtmlAttributeName("authorized-only")]
    public bool AuthorizedOnly { get; set; } = true;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!MFAFor.MFACompleted)
            output.SuppressOutput();
    }
}