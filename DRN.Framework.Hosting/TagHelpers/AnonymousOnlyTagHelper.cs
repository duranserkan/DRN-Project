using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

/// <summary>Requires an anonymous user whenever the anonymous-only attribute is present, regardless of its value.</summary>
[HtmlTargetElement("*", Attributes = "anonymous-only")]
public class AnonymousOnlyTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("anonymous-only");

        if (ScopeContext.Authenticated)
            output.SuppressOutput();
    }
}
