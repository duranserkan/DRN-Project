using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

[HtmlTargetElement("*", Attributes = "anonymous-only")]
public class AnonymousOnlyTagHelper : TagHelper
{
    /// <summary>
    /// Indicates that the link should only be rendered if the user meets certain security conditions.
    /// </summary>
    [HtmlAttributeName("anonymous-only")]
    public bool AnonymousOnly { get; set; } = true;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ScopeContext.Authenticated)
            output.SuppressOutput();
    }
}