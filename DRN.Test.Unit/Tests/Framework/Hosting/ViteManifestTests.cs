using DRN.Framework.Hosting.Utils.Vite;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class ViteManifestTests
{
    [Theory]
    [DataInlineUnit]
    public void GetManifestItem_Should_Preserve_Query_Qualified_Entries(DrnTestContextUnit context)
    {
        var root = CreateManifestRoot(context);
        try
        {
            var outputDir = Path.Combine(root, "app");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "worker.js"), "worker");
            File.WriteAllText(Path.Combine(outputDir, "url.js"), "url");
            WriteManifest(outputDir, """
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

            var manifest = new ViteManifest { ManifestRootPath = root };

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
        finally
        {
            DeleteManifestRoot(root);
        }
    }

    [Theory]
    [DataInlineUnit]
    public void GetAllManifestItems_Should_Ignore_Non_Vite_Manifest_Files(DrnTestContextUnit context)
    {
        var root = CreateManifestRoot(context);
        try
        {
            File.WriteAllText(Path.Combine(root, "manifest.json"), """
                                                                   {
                                                                     "name": "Sample Hosted",
                                                                     "start_url": "/"
                                                                   }
                                                                   """);

            var outputDir = Path.Combine(root, "app");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "app.js"), "app");
            WriteManifest(outputDir, """
                                     {
                                       "buildwww/app/js/app.js": {
                                         "file": "app.js",
                                         "src": "buildwww/app/js/app.js"
                                       }
                                     }
                                     """);

            var manifest = new ViteManifest { ManifestRootPath = root };

            var items = manifest.GetAllManifestItems();

            items.Should().Contain(item => item.Src == "buildwww/app/js/app.js" && item.Path == "/app/app.js");
            manifest.GetManifestItem("name").Should().BeNull();
            manifest.GetManifestItem("buildwww/app/js/app.js")!.Path.Should().Be("/app/app.js");
        }
        finally
        {
            DeleteManifestRoot(root);
        }
    }

    [Theory]
    [DataInlineUnit]
    public void GetAllManifestItems_Should_Throw_ConfigurationException_When_Manifest_File_Is_Missing(DrnTestContextUnit context)
    {
        var root = CreateManifestRoot(context);
        try
        {
            var outputDir = Path.Combine(root, "app");
            Directory.CreateDirectory(outputDir);
            WriteManifest(outputDir, """
                                     {
                                       "buildwww/app/js/missing.js": {
                                         "file": "missing.js",
                                         "src": "buildwww/app/js/missing.js"
                                       }
                                     }
                                     """);

            var manifest = new ViteManifest { ManifestRootPath = root };

            var act = manifest.GetAllManifestItems;

            act.Should().ThrowExactly<ConfigurationException>()
                .WithMessage("*Vite manifest at*manifest.json*references missing file: missing.js*resolved path:*");
        }
        finally
        {
            DeleteManifestRoot(root);
        }
    }

    [Theory]
    [DataInlineUnit]
    public void GetAllManifestItems_Should_Throw_ConfigurationException_When_Manifest_File_Escapes_Output_Directory(DrnTestContextUnit context)
    {
        var root = CreateManifestRoot(context);
        try
        {
            var outputDir = Path.Combine(root, "app");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(root, "outside.js"), "outside");
            WriteManifest(outputDir, """
                                     {
                                       "buildwww/app/js/outside.js": {
                                         "file": "../outside.js",
                                         "src": "buildwww/app/js/outside.js"
                                       }
                                     }
                                     """);

            var manifest = new ViteManifest { ManifestRootPath = root };

            var act = manifest.GetAllManifestItems;

            act.Should().ThrowExactly<ConfigurationException>()
                .WithMessage("*Vite manifest at*manifest.json*outside its output directory: ../outside.js*");
        }
        finally
        {
            DeleteManifestRoot(root);
        }
    }

    private static string CreateManifestRoot(DrnTestContextUnit context)
    {
        var root = Path.Combine(context.MethodContext.GetTestFolderLocation(), "ViteManifestTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteManifest(string outputDir, string manifestJson)
    {
        var manifestDir = Path.Combine(outputDir, ".vite");
        Directory.CreateDirectory(manifestDir);
        File.WriteAllText(Path.Combine(manifestDir, "manifest.json"), manifestJson);
    }

    private static void DeleteManifestRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }
}
