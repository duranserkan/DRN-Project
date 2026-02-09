using DRN.Framework.Hosting.Utils;
using Sample.Hosted;

namespace DRN.Test.Integration.Tests.Framework.Hosting.BackgroundServices;

public class StaticAssetPreWarmServiceTests
{

    [Theory]
    [DataInline]
    public async Task ViteManifest_GetAllManifestItems_Should_Return_Collection_When_Application_Started(DrnTestContext context)
    {
        ViteManifest.IsViteOrigin("buildwww/assets/main.js").Should().BeTrue();
        ViteManifest.IsViteOrigin("node_modules/vue/dist/vue.js").Should().BeTrue();
        ViteManifest.IsViteOrigin("BUILDWWW/app.css").Should().BeTrue();
        ViteManifest.IsViteOrigin("NODE_MODULES/react/index.js").Should().BeTrue();
        
        ViteManifest.IsViteOrigin("/app/static/main.js").Should().BeFalse();
        ViteManifest.IsViteOrigin("/assets/styles.css").Should().BeFalse();
        ViteManifest.IsViteOrigin("assets/image.png").Should().BeFalse();
        
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        var items = ViteManifest.GetAllManifestItems();
        items.Should().NotBeNull();
        
        var item = ViteManifest.GetManifestItem("non-existent-entry");
        item.Should().BeNull();
        
        var report = ViteManifest.PreWarmReport;
        report.Should().Match<ViteManifestPreWarmReport?>(r => r == null || r.TotalAssets >= 0);
    }
}
