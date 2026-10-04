using System.Text.Encodings.Web;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.Hosting.TagHelpers;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class ApplicationUrlTests
{
    [Theory]
    [DataInlineUnit("", "/Api/Sample/Report", "/Api/Sample/Report")]
    [DataInlineUnit("/api", "/Api/Sample/Report", "/api/Api/Sample/Report")]
    [DataInlineUnit("/Api", "/Api/Sample/Report", "/Api/Api/Sample/Report")]
    [DataInlineUnit("/gateway/api", "~/User/Login?x=%2Fitems&v=1#form", "/gateway/api/User/Login?x=%2Fitems&v=1#form")]
    [DataInlineUnit("/api", "https://example.com/path", "https://example.com/path")]
    [DataInlineUnit("/api", "//example.com/path", "//example.com/path")]
    [DataInlineUnit("/api", "/\\example.com/path", "/\\example.com/path")]
    [DataInlineUnit("/api", "#section", "#section")]
    [DataInlineUnit("/api", "relative/path", "relative/path")]
    public void Application_Routes_Should_Preserve_Controller_Prefixes(string pathBase, string path, string expected)
    {
        var request = new DefaultHttpContext().Request;
        request.PathBase = pathBase;

        request.ApplicationUrl(path).Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit("a", "href")]
    [DataInlineUnit("img", "src")]
    [DataInlineUnit("form", "action")]
    [DataInlineUnit("button", "formaction")]
    [DataInlineUnit("button", "hx-get")]
    [DataInlineUnit("form", "hx-post")]
    [DataInlineUnit("button", "hx-put")]
    [DataInlineUnit("button", "hx-patch")]
    [DataInlineUnit("button", "hx-delete")]
    public void Razor_Application_Urls_Should_Resolve_Once_And_Preserve_Encoding(string tag, string attribute)
    {
        var view = CreateViewContext("/api");
        var helper = new ApplicationUrlTagHelper(new UrlHelperFactory(), HtmlEncoder.Default) { ViewContext = view };
        var output = CreateOutput(tag, attribute, new HtmlString("~/Api/Sample/Report?x=1&amp;y=2#form"));
        var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "urls");

        helper.Process(context, output);
        helper.Process(context, output);

        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        writer.ToString().Should().Contain("/api/Api/Sample/Report?x=1&amp;y=2#form")
            .And.NotContain("&amp;amp;").And.NotContain("/api/api/");
    }

    [Theory]
    [DataInlineUnit("/api/Api/Report")]
    [DataInlineUnit("/Api/Report")]
    [DataInlineUnit("https://example.com/report")]
    [DataInlineUnit("//example.com/report")]
    [DataInlineUnit("#section")]
    [DataInlineUnit("relative/report")]
    public void Razor_Should_Only_Resolve_Explicit_Application_Urls(string url)
    {
        var helper = new ApplicationUrlTagHelper(new UrlHelperFactory(), HtmlEncoder.Default) { ViewContext = CreateViewContext("/api") };
        var output = CreateOutput("button", "hx-get", url);
        helper.Process(new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "urls"), output);
        output.Attributes["hx-get"].Value.Should().Be(url);
    }

    [Theory]
    [DataInlineUnit("", "/profile", "~/profile", true, false)]
    [DataInlineUnit("/api", "/profile", "~/profile", true, false)]
    [DataInlineUnit("/api", "/profile", "~/profile?x=1&amp;y=2#details", true, true)]
    [DataInlineUnit("/api", "/profile", "/api/profile", true, false)]
    [DataInlineUnit("/api", "/profile", "/api/profile/", true, false)]
    [DataInlineUnit("/api", "/profile & details", "~/profile%20%26%20details", true, true)]
    [DataInlineUnit("/api", "/profile", "/profile", false, false)]
    [DataInlineUnit("/api", "/profile", "~/User/Profile", false, false)]
    [DataInlineUnit("/api", "/profile", "~/other", false, false)]
    [DataInlineUnit("/api", "/profile", "https://example.com/api/profile", false, false)]
    [DataInlineUnit("/api", "/profile", "//example.com/api/profile", false, false)]
    [DataInlineUnit("/api", "/profile", "/\\example.com/api/profile", false, false)]
    [DataInlineUnit("/api", "/profile", "#details", false, false)]
    [DataInlineUnit("/api", "/profile", "profile", false, false)]
    public void Active_Links_Should_Match_Resolved_Local_Request_Paths(
        string pathBase, string path, string href, bool active, bool htmlContent)
    {
        var view = CreateViewContext(pathBase);
        view.HttpContext.Request.Path = path;
        view.ActionDescriptor.RouteValues["page"] = "/User/Profile";
        var output = CreateOutput("a", "href", htmlContent ? new HtmlString(href) : href);
        var context = new TagHelperContext(new TagHelperAttributeList { { "href", href } }, new Dictionary<object, object>(), "active");

        new ApplicationUrlTagHelper(new UrlHelperFactory(), HtmlEncoder.Default) { ViewContext = view }.Process(context, output);
        new PageAnchorHrefTagHelper { ViewContext = view }.Process(context, output);

        output.Attributes.ContainsName("aria-current").Should().Be(active);
        output.Attributes.ContainsName("class").Should().Be(active);
        if (active)
        {
            output.Attributes["aria-current"].Value.Should().Be("page");
            output.Attributes["class"].Value.Should().Be("active fw-bold");
        }
    }

    [Fact]
    public void Page_Links_Should_Match_Page_Identity_With_A_Custom_Public_Path()
    {
        var view = CreateViewContext("/api");
        view.HttpContext.Request.Path = "/profile";
        view.ActionDescriptor.RouteValues["page"] = "/User/Profile";
        var output = CreateOutput("a", "asp-page", "/User/Profile");
        var context = new TagHelperContext(new TagHelperAttributeList { { "asp-page", "/User/Profile" } },
            new Dictionary<object, object>(), "active-page");

        new PageAnchorAspPageTagHelper { ViewContext = view }.Process(context, output);

        output.Attributes["aria-current"].Value.Should().Be("page");
    }

    private static ViewContext CreateViewContext(string pathBase)
    {
        var view = new ViewContext { HttpContext = new DefaultHttpContext(), RouteData = new RouteData(), ActionDescriptor = new ActionDescriptor() };
        view.HttpContext.Request.PathBase = pathBase;
        return view;
    }

    private static TagHelperOutput CreateOutput(string tag, string attribute, object value) =>
        new(tag, new TagHelperAttributeList { { attribute, value } },
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
}
