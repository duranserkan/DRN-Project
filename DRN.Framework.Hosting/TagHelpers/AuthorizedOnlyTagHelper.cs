using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

/// <summary>Requires an authenticated user whenever the authorized-only attribute is present, regardless of its value.</summary>
[HtmlTargetElement("*", Attributes = "authorized-only")]
public class AuthorizedOnlyTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("authorized-only");

        if (!ScopeContext.Authenticated)
            output.SuppressOutput();
    }
}
