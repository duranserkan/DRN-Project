using System.Text.Json;
using DRN.Framework.SharedKernel.Conventions;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Settings.Conventions;

namespace DRN.Test.Tests.Framework.Hosting;

public class ConfigurationExtensionTests
{
    [Theory]
    [DataInline]
    public void MountDirectorySettings_Should_Be_Added(TestContext context, IMountedSettingsConventionsOverride conventionsOverride)
    {
        var testFolder = context.MethodContext.GetTestFolderLocation();
        conventionsOverride.MountedSettingsDirectory.Returns(Path.Combine(testFolder, "MountDir"));

        var appsettings = context.GetRequiredService<IAppSettings>();
        appsettings.Environment.Should().Be(AppEnvironment.Staging);

        var password = appsettings.GetValue<string>("postgres-password");
        password.Should().Be("Be Always Progressive: Follow the Mustafa Kemal Atatürk's Enlightenment Ideals");

        var autoMigrate = appsettings.GetValue<bool>("DrnContext_AutoMigrateDevEnvironment");
        autoMigrate.Should().BeTrue();

        var summaryJson = JsonSerializer.Serialize(appsettings.GetDebugView().ToSummary());
    }
}