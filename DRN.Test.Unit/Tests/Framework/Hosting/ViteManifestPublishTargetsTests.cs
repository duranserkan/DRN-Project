using System.Xml.Linq;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class ViteManifestPublishTargetsTests
{
    [Theory]
    [DataInlineUnit]
    public void HostingTargets_Should_Define_Vite_Manifest_Publish_Items_For_Web_Sdk_Projects(DrnTestContextUnit context)
    {
        _ = context;
        var document = XDocument.Load(FindHostingTargetsFile());

        var publishToggle = document.Descendants("DrnHostingViteManifestPublishItemsEnabled").Single();
        publishToggle.Value.Should().Be("true");
        publishToggle.Attribute("Condition")?.Value.Should().Be("'$(DrnHostingViteManifestPublishItemsEnabled)' == ''");

        var target = document.Descendants("Target")
            .Single(element => element.Attribute("Name")?.Value == "DrnHostingIncludeViteManifestPublishItems");
        target.Attribute("Condition")?.Value.Should()
            .Be("'$(UsingMicrosoftNETSdkWeb)' == 'true' and '$(DrnHostingViteManifestPublishItemsEnabled)' == 'true'");
        var beforeTargets = target.Attribute("BeforeTargets")?.Value.Split(';') ?? [];
        beforeTargets.Should().Equal("AssignTargetPaths", "GetCopyToPublishDirectoryItems", "ComputeFilesToPublish");

        var manifestFiles = target.Descendants("_DrnHostingViteManifestPublishFile").Single();
        manifestFiles.Attribute("Include")?.Value.Should().Be("$(MSBuildProjectDirectory)/wwwroot/**/.vite/manifest.json");
        manifestFiles.Attribute("Exclude")?.Value.Should().Be("@(Content->'%(FullPath)')");

        var content = target.Descendants("Content").Single();
        content.Attribute("Include")?.Value.Should().Be("@(_DrnHostingViteManifestPublishFile)");
        content.Attribute("Link").Should().BeNull();
        content.Attribute("TargetPath").Should().BeNull();
        content.Attribute("CopyToOutputDirectory")?.Value.Should().Be("PreserveNewest");
        content.Attribute("CopyToPublishDirectory")?.Value.Should().Be("PreserveNewest");
    }

    private static string FindHostingTargetsFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var targetsFile = Path.Combine(
                directory.FullName,
                "DRN.Framework.Hosting",
                "buildTransitive",
                "DRN.Framework.Hosting.targets");

            if (File.Exists(targetsFile))
                return targetsFile;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find DRN.Framework.Hosting.targets from the test output directory.");
    }
}
