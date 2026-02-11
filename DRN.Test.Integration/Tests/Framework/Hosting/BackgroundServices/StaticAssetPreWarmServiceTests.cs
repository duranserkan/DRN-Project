using DRN.Framework.Hosting.BackgroundServices.StaticAssetPreWarm;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.Hosting.Utils.Vite.Models;
using Sample.Hosted;

namespace DRN.Test.Integration.Tests.Framework.Hosting.BackgroundServices;

public class StaticAssetPreWarmServiceTests
{
    [Theory]
    [DataInline]
    public async Task ViteManifest_GetAllManifestItems_Should_Return_Collection_When_Application_Started
        (DrnTestContext context, IStaticAssetPreWarmProxyClientFactory factory)
    {
        ViteManifest.IsViteOrigin("buildwww/assets/main.js").Should().BeTrue();
        ViteManifest.IsViteOrigin("node_modules/vue/dist/vue.js").Should().BeTrue();
        ViteManifest.IsViteOrigin("BUILDWWW/app.css").Should().BeTrue();
        ViteManifest.IsViteOrigin("NODE_MODULES/react/index.js").Should().BeTrue();

        ViteManifest.IsViteOrigin("/wwwroot/static/main.js").Should().BeFalse();
        ViteManifest.IsViteOrigin("/app/static/main.js").Should().BeFalse();
        ViteManifest.IsViteOrigin("/assets/styles.css").Should().BeFalse();
        ViteManifest.IsViteOrigin("assets/image.png").Should().BeFalse();
        context.AddToConfiguration(StaticAssetPreWarmService.EnablePrewarmForTestKey, "true");

        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        factory.GetClient(TestEnvironment.TestContextAddress).Returns(client);
        var mockClient = factory.GetClient(TestEnvironment.TestContextAddress);
        mockClient.Should().Be(client);

        var viteManifest = context.GetRequiredService<IViteManifest>();
        var items = viteManifest.GetAllManifestItems();
        items.Should().NotBeNull();

        var item = viteManifest.GetManifestItem("non-existent-entry");
        item.Should().BeNull();

        var report = await WaitForPrewarmReportAsync(viteManifest);

        report.Should().NotBeNull();
        report.TotalAssets.Should().BeGreaterThanOrEqualTo(0);
    }

    private static async Task<ViteManifestPreWarmReport?> WaitForPrewarmReportAsync(IViteManifest viteManifest)
    {
        ViteManifestPreWarmReport? report = null;
        var timeout = TimeSpan.FromSeconds(8);
        var interval = TimeSpan.FromMilliseconds(25);
        var elapsed = TimeSpan.Zero;
        while (elapsed < timeout)
        {
            report = viteManifest.PreWarmReport;
            if (report != null)
                break;

            await Task.Delay(interval);
            elapsed += interval;
        }

        return report;
    }
}