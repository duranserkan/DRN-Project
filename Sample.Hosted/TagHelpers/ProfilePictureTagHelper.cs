using DRN.Framework.Utils.Scope;
using DRN.Framework.Hosting.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Sample.Hosted.Helpers;

namespace Sample.Hosted.TagHelpers;

[HtmlTargetElement("profile-picture")]
public class ProfilePictureTagHelper(IUrlHelperFactory urlHelperFactory) : TagHelper
{
    public string? Alt { get; set; }
    public string? Class { get; set; }
    public string? Style { get; set; }

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // Set up the tag as an <img> element
        output.TagName = "img";
        output.TagMode = TagMode.SelfClosing;

        // Include the user id so browser caches are partitioned by account even when PPVersion values match.
        var src = urlHelperFactory.GetUrlHelper(ViewContext).Endpoint(Get.Endpoint.User.PP.Get,
            new { userId = ScopeContext.UserId, v = Get.Claim.Profile.PPVersion });
        output.Attributes.SetAttribute("src", src);
        output.Attributes.SetAttribute("alt", Alt ?? "Profile Picture");

        if (!string.IsNullOrWhiteSpace(Class))
            output.Attributes.SetAttribute("class", Class);

        if (string.IsNullOrWhiteSpace(Style))
            Style = "border: 0.2rem solid #ddd; border-radius: 2px; box-shadow: 0 2px 5px rgba(0, 0, 0, 0.1);";

        output.Attributes.SetAttribute("style", Style);
    }
}
