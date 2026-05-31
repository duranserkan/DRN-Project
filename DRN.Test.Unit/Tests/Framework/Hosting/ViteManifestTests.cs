using DRN.Framework.Hosting.Utils.Vite;
using Microsoft.AspNetCore.Hosting;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class ViteManifestTests
{
    [Theory]
    [DataInlineUnit]
    public void GetManifestItem_Should_Preserve_Query_Qualified_Entries(DrnTestContextUnit context)
    {
        using var helper = new ViteManifestTestHelper(context);
        var outputDir = Path.Combine(helper.Root, "app");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "worker.js"), "worker");
        File.WriteAllText(Path.Combine(outputDir, "url.js"), "url");
        helper.WriteManifest(outputDir, """
                                        {
                                          "buildwww/app/js/app.js?worker": {
                                            "file": "worker.js",
                                            "src": "buildwww/app/js/app.js?worker"
                                          },
                                          "buildwww/app/js/app.js?url": {
                                            "file": "url.js",
                                            "src": "buildwww/app/js/app.js?url"
                                          }
                                        }
                                        """);

        var manifest = helper.CreateManifest();

        var worker = manifest.GetManifestItem("buildwww/app/js/app.js?worker");
        var url = manifest.GetManifestItem("buildwww/app/js/app.js?url");

        worker.Should().NotBeNull();
        worker!.Path.Should().Be("/app/worker.js");
        worker.Hash.Should().NotBeEmpty();
        worker.Integrity.Should().StartWith("sha256-");

        url.Should().NotBeNull();
        url!.Path.Should().Be("/app/url.js");
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
        helper.WriteManifest(outputDir, """
                                        {
                                          "buildwww/app/js/app.js": {
                                            "file": "app.js",
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
        helper.WriteManifest(outputDir, """
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
        helper.WriteManifest(outputDir, """
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

        public void WriteManifest(string outputDir, string manifestJson)
        {
            var manifestDir = Path.Combine(outputDir, ".vite");
            Directory.CreateDirectory(manifestDir);
            File.WriteAllText(Path.Combine(manifestDir, "manifest.json"), manifestJson);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}
