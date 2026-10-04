using System.Text.Encodings.Web;
using System.Text.Json;
using DRN.Framework.Hosting.TagHelpers;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.Hosting.Utils.Vite.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class ViteTagHelperTests
{
    [Theory]
    [DataInlineUnit("", "/app/app.abc123.css")]
    [DataInlineUnit("/", "/app/app.abc123.css")]
    [DataInlineUnit("/api", "/api/app/app.abc123.css")]
    [DataInlineUnit("/api/", "/api/app/app.abc123.css")]
    [DataInlineUnit("/gateway/api", "/gateway/api/app/app.abc123.css")]
    public void Links_Should_Respect_PathBase_And_Preserve_Integrity(string pathBase, string expected)
    {
        const string source = "buildwww/app/css/app.css";
        var item = CreateItem("app.abc123.css", source);
        var manifest = Substitute.For<IViteManifest>();
        manifest.GetManifestItem(source).Returns(item);
        var viewContext = CreateViewContext(pathBase);

        foreach (var rel in new[] { "stylesheet", "preload" })
        {
            var output = CreateOutput("link");
            output.Attributes.Add("rel", rel);
            var helper = new ViteLinkTagHelper(manifest) { Href = source, ViewContext = viewContext };

            helper.Process(CreateContext(), output);
            ResolveApplicationUrls(viewContext, output);

            output.Attributes["href"].Value.Should().Be(expected);
            output.Attributes["integrity"].Value.Should().Be(item.Integrity);
            output.Attributes["rel"].Value.Should().Be(rel);
            output.Attributes.Count(attribute => attribute.Name == "href").Should().Be(1);
        }
        item.Path.Should().Be("/app/app.abc123.css");
    }

    [Theory]
    [DataInlineUnit("", "/app/app.abc123.js")]
    [DataInlineUnit("/api", "/api/app/app.abc123.js")]
    [DataInlineUnit("/api/", "/api/app/app.abc123.js")]
    public void Scripts_Should_Resolve_Per_Request_Without_Changing_Shared_Manifest(string pathBase, string expected)
    {
        const string source = "buildwww/app/js/app.js";
        var item = CreateItem("app.abc123.js", source);
        var manifest = Substitute.For<IViteManifest>();
        manifest.GetManifestItem(source).Returns(item);
        var viewContext = CreateViewContext(pathBase);
        var helper = new ViteScriptTagHelper(manifest) { Src = source, ViewContext = viewContext };
        var output = CreateOutput("script");

        helper.Process(CreateContext(), output);
        ResolveApplicationUrls(viewContext, output);

        output.Attributes["src"].Value.Should().Be(expected);
        output.Attributes["integrity"].Value.Should().Be(item.Integrity);
        output.Attributes.Count(attribute => attribute.Name == "src").Should().Be(1);

        var preload = CreateOutput("link");
        preload.Attributes.Add("rel", "modulepreload");
        new ViteLinkTagHelper(manifest) { Href = source, ViewContext = viewContext }.Process(CreateContext(), preload);
        ResolveApplicationUrls(viewContext, preload);
        preload.Attributes["href"].Value.Should().Be(expected);
        preload.Attributes["integrity"].Value.Should().Be(item.Integrity);
        preload.Attributes["rel"].Value.Should().Be("modulepreload");

        viewContext.HttpContext.Request.PathBase = "/other";
        var otherOutput = CreateOutput("script");
        helper.Process(CreateContext(), otherOutput);
        otherOutput.Attributes["src"].Value.Should().Be("/other/app/app.abc123.js");
        item.Path.Should().Be("/app/app.abc123.js");
    }

    [Theory]
    [DataInlineUnit("https://cdn.example.com/app.js?v=1#asset")]
    [DataInlineUnit("//cdn.example.com/app.js")]
    [DataInlineUnit("/api/app/app.js")]
    [DataInlineUnit("/apiary/app.js")]
    [DataInlineUnit("relative/app.js")]
    [DataInlineUnit("data:text/javascript,void(0)")]
    public void Non_Vite_Urls_Should_Remain_Unchanged(string url)
    {
        var manifest = Substitute.For<IViteManifest>();
        foreach (var pathBase in new[] { "", "/api" })
        {
            var viewContext = CreateViewContext(pathBase);
            var link = CreateOutput("link");
            var script = CreateOutput("script");
            link.Attributes.Add("integrity", "sha256-existing");
            script.Attributes.Add("integrity", "sha256-existing");

            new ViteLinkTagHelper(manifest) { Href = url, ViewContext = viewContext }.Process(CreateContext(), link);
            new ViteScriptTagHelper(manifest) { Src = url, ViewContext = viewContext }.Process(CreateContext(), script);
            ResolveApplicationUrls(viewContext, link);
            ResolveApplicationUrls(viewContext, script);

            link.Attributes["href"].Value.Should().Be(url);
            script.Attributes["src"].Value.Should().Be(url);
            link.Attributes["integrity"].Value.Should().Be("sha256-existing");
            script.Attributes["integrity"].Value.Should().Be("sha256-existing");
        }
        manifest.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("", "/images/100.jpeg")]
    [DataInlineUnit("/api", "/api/images/100.jpeg")]
    public void Application_Relative_Favicon_Should_Resolve_Once(string pathBase, string expected)
    {
        var manifest = Substitute.For<IViteManifest>();
        var viewContext = CreateViewContext(pathBase);
        const string href = "~/images/100.jpeg";
        var output = CreateOutput("link");
        output.Attributes.Add("rel", "icon");

        new ViteLinkTagHelper(manifest) { Href = href, ViewContext = viewContext }.Process(CreateContext(), output);
        ResolveApplicationUrls(viewContext, output);

        output.Attributes["href"].Value.Should().Be(expected);
        output.Attributes.ContainsName("integrity").Should().BeFalse();
        manifest.ReceivedCalls().Should().BeEmpty();
    }

    private static ViteManifestItem CreateItem(string file, string source) =>
        JsonSerializer.Deserialize<ViteManifestItem>(JsonSerializer.Serialize(new
        {
            File = file, Src = source, OutputDir = "/app/", Hash = "dGVzdA=="
        }))!;

    private static ViewContext CreateViewContext(string pathBase)
    {
        var context = new ViewContext { HttpContext = new DefaultHttpContext(), RouteData = new RouteData() };
        context.HttpContext.Request.PathBase = pathBase;
        context.HttpContext.Request.Path = "/nested/page";
        return context;
    }

    private static void ResolveApplicationUrls(ViewContext viewContext, TagHelperOutput output) =>
        new UrlResolutionTagHelper(new UrlHelperFactory(), HtmlEncoder.Default) { ViewContext = viewContext }
            .Process(CreateContext(), output);

    private static TagHelperContext CreateContext() => new(new TagHelperAttributeList(), new Dictionary<object, object>(), "vite");

    private static TagHelperOutput CreateOutput(string tagName) => new(tagName, new TagHelperAttributeList(),
        (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
}
