using System.Text.Json;
using DRN.Framework.Hosting.DrnProgram.Configurators;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.Hosting.Utils.Vite.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class ViteManifestTests
{
    [Fact]
    public void IsViteOrigin_Should_Match_Only_Build_And_Package_Prefixes_Ignoring_Case()
    {
        ViteManifest.IsViteOrigin("buildwww/assets/main.js").Should().BeTrue();
        ViteManifest.IsViteOrigin("node_modules/vue/dist/vue.js").Should().BeTrue();
        ViteManifest.IsViteOrigin("BUILDWWW/app.css").Should().BeTrue();
        ViteManifest.IsViteOrigin("NODE_MODULES/react/index.js").Should().BeTrue();

        ViteManifest.IsViteOrigin("/wwwroot/static/main.js").Should().BeFalse();
        ViteManifest.IsViteOrigin("/app/static/main.js").Should().BeFalse();
        ViteManifest.IsViteOrigin("/assets/styles.css").Should().BeFalse();
        ViteManifest.IsViteOrigin("assets/image.png").Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit]
    public void GetManifestItem_Should_Preserve_Query_Qualified_Entries(DrnTestContextUnit context)
    {
        using var helper = new ViteManifestTestHelper(context);
        var outputDir = Path.Combine(helper.Root, "app");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "worker.js.br"), "sidecar");
        File.WriteAllText(Path.Combine(outputDir, "url.js"), "url");
        WriteManifest(outputDir, """
                                        {
                                          "buildwww/app/js/app.js?worker": {
                                            "file": "worker.js",
                                            "brotliOnly": true,
                                            "hash": "1NVXzfSuJNRNtBDa7nGdcQMIPCr0f7f4i5zoLNzdJ/c=",
                                            "src": "buildwww/app/js/app.js?worker"
                                          },
                                          "buildwww/app/js/app.js?url": {
                                            "file": "url.js",
                                            "hash": "1NVXzfSuJNRNtBDa7nGdcQMIPCr0f7f4i5zoLNzdJ/c=",
                                            "src": "buildwww/app/js/app.js?url"
                                          }
                                        }
                                        """);

        var manifest = helper.CreateManifest();

        var worker = manifest.GetManifestItem("buildwww/app/js/app.js?worker");
        var url = manifest.GetManifestItem("buildwww/app/js/app.js?url");

        worker.Should().NotBeNull();
        worker.Path.Should().Be("/app/worker.js");
        worker.BrowserPath.Should().Be("/app/worker.js.br");
        worker.Integrity.Should().Be("sha256-1NVXzfSuJNRNtBDa7nGdcQMIPCr0f7f4i5zoLNzdJ/c=");

        url.Should().NotBeNull();
        url.Path.Should().Be("/app/url.js");
        url.BrowserPath.Should().Be(url.Path);
        manifest.GetManifestItem("buildwww/app/js/app.js").Should().BeNull();
    }

    [Theory]
    [DataInlineUnit]
    public void GetAllManifestItems_Should_Ignore_Non_Vite_Manifest_Files(DrnTestContextUnit context)
    {
        using var helper = new ViteManifestTestHelper(context);
        File.WriteAllText(Path.Combine(helper.Root, "manifest.json"), """
                                                                      {
                                                                        "name": "Sample Hosted",
                                                                        "start_url": "/"
                                                                      }
                                                                      """);

        var outputDir = Path.Combine(helper.Root, "app");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "app.js"), "app");
        WriteManifest(outputDir, """
                                        {
                                          "buildwww/app/js/app.js": {
                                            "file": "app.js",
                                            "hash": "1NVXzfSuJNRNtBDa7nGdcQMIPCr0f7f4i5zoLNzdJ/c=",
                                            "src": "buildwww/app/js/app.js"
                                          }
                                        }
                                        """);

        var manifest = helper.CreateManifest();

        var items = manifest.GetAllManifestItems();

        items.Should().Contain(item => item.Src == "buildwww/app/js/app.js" && item.Path == "/app/app.js");
        manifest.GetManifestItem("name").Should().BeNull();
        manifest.GetManifestItem("buildwww/app/js/app.js")!.Path.Should().Be("/app/app.js");
    }

    [Theory]
    [DataInlineUnit]
    public void GetAllManifestItems_Should_Throw_ConfigurationException_When_Manifest_File_Is_Missing(DrnTestContextUnit context)
    {
        using var helper = new ViteManifestTestHelper(context);
        var outputDir = Path.Combine(helper.Root, "app");
        Directory.CreateDirectory(outputDir);
        WriteManifest(outputDir, """
                                        {
                                          "buildwww/app/js/missing.js": {
                                            "file": "missing.js",
                                            "src": "buildwww/app/js/missing.js"
                                          }
                                        }
                                        """);

        var manifest = helper.CreateManifest();

        var act = manifest.GetAllManifestItems;

        act.Should().ThrowExactly<ConfigurationException>()
            .WithMessage("*Vite manifest at*manifest.json*references missing file: missing.js*resolved path:*");
    }

    [Theory]
    [DataInlineUnit]
    public void GetAllManifestItems_Should_Throw_ConfigurationException_When_Manifest_File_Escapes_Output_Directory(DrnTestContextUnit context)
    {
        using var helper = new ViteManifestTestHelper(context);
        var outputDir = Path.Combine(helper.Root, "app");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(helper.Root, "outside.js"), "outside");
        WriteManifest(outputDir, """
                                        {
                                          "buildwww/app/js/outside.js": {
                                            "file": "../outside.js",
                                            "src": "buildwww/app/js/outside.js"
                                          }
                                        }
                                        """);

        var manifest = helper.CreateManifest();

        var act = manifest.GetAllManifestItems;

        act.Should().ThrowExactly<ConfigurationException>()
            .WithMessage("*Vite manifest at*manifest.json*outside its output directory: ../outside.js*");
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Manifest_Should_Reject_Sidecar_Symlink_Targeting_File_Outside_Web_Root(DrnTestContextUnit context, bool brotliOnly)
    {
        using var helper = new ViteManifestTestHelper(context);
        var outside = Path.Combine(context.GetTempPath(), "outside.js.br");
        File.WriteAllText(outside, "external fixture");
        try
        {
            File.CreateSymbolicLink(Path.Combine(helper.Root, "app.js.br"), outside);
        }
        catch (Exception e) when (e is PlatformNotSupportedException or UnauthorizedAccessException ||
                                  OperatingSystem.IsWindows() && e is IOException)
        {
            Assert.Skip("File symbolic link creation is unavailable in this environment.");
        }

        if (!brotliOnly)
            File.WriteAllText(Path.Combine(helper.Root, "app.js"), "app");
        WriteManifest(helper.Root, JsonSerializer.Serialize(new
        {
            app = new { file = "app.js", src = "app", brotliOnly, hash = "1NVXzfSuJNRNtBDa7nGdcQMIPCr0f7f4i5zoLNzdJ/c=" }
        }));

        var act = helper.CreateManifest().GetAllManifestItems;
        act.Should().ThrowExactly<ConfigurationException>()
            .WithMessage("*manifest.json*outside its output directory: app.js.br");
    }

    [Theory]
    [DataInlineUnit("")]
    [DataInlineUnit("/static")]
    public async Task StaticFiles_Should_Map_Only_Listed_Brotli_Sidecars_To_The_Original_Mime_Type(DrnTestContextUnit context, string requestPath)
    {
        using var helper = new ViteManifestTestHelper(context);
        var output = Directory.CreateDirectory(Path.Combine(helper.Root, "assets"));
        foreach (var file in new[] { "app.css", "app.css.br", "unlisted.css", "unlisted.css.br" })
            await File.WriteAllTextAsync(Path.Combine(output.FullName, file), "fixture");
        WriteManifest(output.FullName, """{"style":{"file":"app.css","src":"style","hash":"1NVXzfSuJNRNtBDa7nGdcQMIPCr0f7f4i5zoLNzdJ/c="}}""");
        var options = new StaticFileOptions { RequestPath = requestPath };
        DrnStaticFileConfigurator.ConfigureStaticFileOptions(options);
        DrnStaticFileConfigurator.ConfigureViteSidecars(options, helper.CreateManifest());
        var rules = new DrnStaticFileHeaderOptions();
        rules.Set(new("/assets/app.css.br", "X-Asset", "style"));
        DrnStaticFileConfigurator.ConfigureHeaderRules(options, Options.Create(rules));

        var types = options.ContentTypeProvider;
        types.TryGetContentType("/assets/app.css.br", out var type).Should().BeTrue();
        type.Should().Be("text/css");
        types.TryGetContentType("/assets/unlisted.css.br", out _).Should().BeFalse();
        types.TryGetContentType("/assets/APP.css.br", out _).Should().BeFalse();

        var http = new DefaultHttpContext
        {
            Request =
            {
                Path = requestPath + "/assets/app.css.br"
            }
        };
        var response = new StaticFileResponseContext(http, new NotFoundFileInfo("fixture"));
        options.OnPrepareResponse(response);
        await options.OnPrepareResponseAsync(response);
        http.Response.Headers.ContentEncoding.ToString().Should().Be("br");
        http.Response.Headers.CacheControl.ToString().Should().Be("public,max-age=31536000");
        http.Response.Headers["X-Asset"].ToString().Should().Be("style");
    }

    [Fact]
    public void StaticFile_Sidecar_Callbacks_Should_Not_Allocate_After_Initialization()
    {
        var item = JsonSerializer.Deserialize<ViteManifestItem>("""{"file":"app.css","src":"style"}""")!;
        var manifest = Substitute.For<IViteManifest>();
        manifest.GetAllManifestItems().Returns([item, item]);
        var original = new TrackingContentTypeProvider();
        var options = new StaticFileOptions { RequestPath = "/static", ContentTypeProvider = original };
        DrnStaticFileConfigurator.ConfigureStaticFileOptions(options);
        DrnStaticFileConfigurator.ConfigureViteSidecars(options, manifest);
        var types = options.ContentTypeProvider;
        types.TryGetContentType("/ordinary.css", out _).Should().BeTrue();
        manifest.DidNotReceive().GetAllManifestItems();

        var http = new DefaultHttpContext
        {
            Request =
            {
                Path = "/static/app.css.br"
            }
        };
        var response = new StaticFileResponseContext(http, new NotFoundFileInfo("fixture"));
        for (var index = 0; index < 1000; index++)
        {
            types.TryGetContentType("/app.css.br", out _);
            options.OnPrepareResponse(response);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++)
        {
            types.TryGetContentType("/app.css.br", out _);
            options.OnPrepareResponse(response);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
        original.LastPath.Should().BeSameAs(item.Path);
        http.Response.Headers.ContentEncoding.ToString().Should().Be("br");
        manifest.Received(1).GetAllManifestItems();
        original.ContentType = "text/x-custom-css";
        types.TryGetContentType("/app.css.br", out var type).Should().BeTrue();
        type.Should().Be("text/x-custom-css");
        types.TryGetContentType("/unlisted.css.br", out _).Should().BeTrue();
        original.LastPath.Should().Be("/unlisted.css.br");
    }

    [Theory]
    [DataInlineUnit]
    public void Manifest_Should_Reject_Missing_Or_Invalid_Build_Hash(DrnTestContextUnit context)
    {
        using var helper = new ViteManifestTestHelper(context);
        File.WriteAllText(Path.Combine(helper.Root, "app.js"), "app");
        foreach (var json in new[] { """{"app":{"file":"app.js","src":"app"}}""", """{"app":{"file":"app.js","src":"app","hash":"invalid"}}""" })
        {
            WriteManifest(helper.Root, json);
            var act = helper.CreateManifest().GetAllManifestItems;
            act.Should().ThrowExactly<ConfigurationException>().WithMessage("*base64 SHA-256 hash for app.js*");
        }
    }

    private static void WriteManifest(string outputDir, string manifestJson)
    {
        var manifestDir = Path.Combine(outputDir, ".vite");
        Directory.CreateDirectory(manifestDir);
        File.WriteAllText(Path.Combine(manifestDir, "manifest.json"), manifestJson);
    }

    private sealed class TrackingContentTypeProvider : IContentTypeProvider
    {
        public string? LastPath { get; private set; }
        public string ContentType { get; set; } = "text/css";

        public bool TryGetContentType(string subpath, out string contentType)
        {
            LastPath = subpath;
            contentType = ContentType;
            return true;
        }
    }

    private sealed class ViteManifestTestHelper : IDisposable
    {
        private readonly IWebHostEnvironment _environment;

        public ViteManifestTestHelper(DrnTestContextUnit context)
        {
            Root = Path.Combine(context.MethodContext.GetTestFolderLocation(), "ViteManifestTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            _environment = Substitute.For<IWebHostEnvironment>();
            _environment.WebRootPath.Returns(Root);
            _environment.ContentRootPath.Returns(Root);
        }

        public string Root { get; }

        public ViteManifest CreateManifest() => new(_environment);

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}
