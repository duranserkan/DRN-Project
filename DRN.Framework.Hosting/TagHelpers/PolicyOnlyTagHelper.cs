using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

/// <summary>Renders an element when the current request user satisfies a named authorization policy.</summary>
[HtmlTargetElement("*", Attributes = "policy-only")]
public class PolicyOnlyTagHelper(IAuthorizationService authorizationService) : TagHelper
{
    /// <summary>The registered policy to evaluate. A non-blank name is required.</summary>
    [HtmlAttributeName("policy-only")]
    public string PolicyOnly { get; set; } = string.Empty;

    /// <summary>The resource supplied to authorization handlers; null when omitted.</summary>
    [HtmlAttributeName("policy-resource")]
    public object? PolicyResource { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PolicyOnly);

        var result = await authorizationService.AuthorizeAsync(ViewContext.HttpContext.User, PolicyResource, PolicyOnly);
        if (!result.Succeeded)
            output.SuppressOutput();
    }
}
