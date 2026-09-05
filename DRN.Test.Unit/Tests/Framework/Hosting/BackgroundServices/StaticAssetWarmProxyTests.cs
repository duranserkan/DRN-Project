using DRN.Framework.Hosting.BackgroundServices.StaticAssetWarm;

namespace DRN.Test.Unit.Tests.Framework.Hosting.BackgroundServices;

public class StaticAssetWarmProxyTests
{
    [Fact]
    public void GetOriginalFileSize_Should_Return_Size_Only_For_Files_Inside_ContentRoot()
    {
        var parent = Directory.CreateTempSubdirectory();
        try
        {
            var contentRoot = Directory.CreateDirectory(Path.Combine(parent.FullName, "wwwroot"));
            var assetDirectory = Directory.CreateDirectory(Path.Combine(contentRoot.FullName, "assets"));
            var assetPath = Path.Combine(assetDirectory.FullName, "app.js");
            File.WriteAllText(assetPath, "asset");
            var outsideDirectory = Directory.CreateDirectory(Path.Combine(parent.FullName, "outside"));
            File.WriteAllText(Path.Combine(outsideDirectory.FullName, "secret.txt"), "secret");

            var size = StaticAssetWarmProxy.GetOriginalFileSize(contentRoot.FullName, "/assets/app.js");
            var outsideSize = StaticAssetWarmProxy.GetOriginalFileSize(contentRoot.FullName, "/../outside/secret.txt");

            size.Should().Be(5);
            outsideSize.Should().Be(0);
        }
        finally
        {
            parent.Delete(true);
        }
    }
}
