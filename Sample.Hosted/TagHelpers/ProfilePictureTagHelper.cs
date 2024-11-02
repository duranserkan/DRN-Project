using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Sample.Hosted.Controllers;
using Sample.Hosted.Utils;

namespace Sample.Hosted.TagHelpers;

[HtmlTargetElement("profile-picture")]
public class ProfilePictureTagHelper : TagHelper
{
    public string? Alt { get; set; }
    public string? Class { get; set; }
    public string? Style { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // Set up the tag as an <img> element
        output.TagName = "img";
        output.TagMode = TagMode.SelfClosing;

        // Set the attributes on the <img> tag
        output.Attributes.SetAttribute("src", $"{SampleEndpointFor.User.PP.ControllerRoute}/{ScopeContext.UserId}?v={ClaimFor.Profile.PPVersion}");
        output.Attributes.SetAttribute("alt", Alt ?? "Profile Picture");

        if (!string.IsNullOrWhiteSpace(Class))
            output.Attributes.SetAttribute("class", Class);

        if (string.IsNullOrWhiteSpace(Style))
            Style = "border: 1px solid #ddd; border-radius: 2px; padding: 2px; box-shadow: 0 2px 5px rgba(0, 0, 0, 0.1);";

        output.Attributes.SetAttribute("style", Style);
    }
}