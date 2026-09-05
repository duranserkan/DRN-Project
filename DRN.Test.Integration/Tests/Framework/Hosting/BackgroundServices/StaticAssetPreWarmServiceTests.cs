using DRN.Framework.Hosting.BackgroundServices.StaticAssetWarm;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.Hosting.Utils.Vite.Models;
using Sample.Hosted;

namespace DRN.Test.Integration.Tests.Framework.Hosting.BackgroundServices;

public class StaticAssetWarmServiceTests
{
    [Theory]
    [DataInline]
    public async Task ViteManifest_GetAllManifestItems_Should_Return_Collection_When_Application_Started
        (DrnTestContext context, IStaticAssetWarmProxyClientFactory factory)
    {
        context.AddToConfiguration(StaticAssetWarmService.EnableWarmForTestKey, "true");

        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        factory.GetClient(TestEnvironment.TestContextAddress).Returns(client);
        var mockClient = factory.GetClient(TestEnvironment.TestContextAddress);
        mockClient.Should().Be(client);

        var viteManifest = context.GetRequiredService<IViteManifest>();
        var items = viteManifest.GetAllManifestItems();
        items.Should().NotBeNullOrEmpty();

        var item = viteManifest.GetManifestItem("non-existent-entry");
        item.Should().BeNull();

        var report = await WaitForWarmReportAsync(viteManifest);

        report.Should().NotBeNull();
        report.TotalAssets.Should().BeGreaterThan(0);
    }

    private static async Task<ViteManifestWarmReport?> WaitForWarmReportAsync(IViteManifest viteManifest)
    {
        ViteManifestWarmReport? report = null;
        var timeout = TimeSpan.FromSeconds(8);
        var interval = TimeSpan.FromMilliseconds(25);
        var elapsed = TimeSpan.Zero;
        while (elapsed < timeout)
        {
            report = viteManifest.WarmReport;
            if (report != null)
                break;

            await Task.Delay(interval);
            elapsed += interval;
        }

        return report;
    }
}
