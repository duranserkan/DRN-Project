using System.Text.Json;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Settings.Conventions;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class ConfigurationExtensionTests
{
    [Theory]
    [DataInlineUnit]
    public void MountDirectorySettings_Should_Be_Added(UnitTestContext context, IMountedSettingsConventionsOverride conventionsOverride)
    {
        var testFolder = context.MethodContext.GetTestFolderLocation();
        conventionsOverride.MountedSettingsDirectory.Returns(Path.Combine(testFolder, "MountDir"));

        var appsettings = context.GetRequiredService<IAppSettings>();
        appsettings.Environment.Should().Be(AppEnvironment.Staging);

        var password = appsettings.GetValue<string>("postgres-password");
        password.Should().Be("Be Always Progressive: Follow the Mustafa Kemal Atatürk's Enlightenment Ideals");

        appsettings.DevelopmentSettings.AutoMigrate.Should().BeTrue();

        var summaryJson = JsonSerializer.Serialize(appsettings.GetDebugView().ToSummary());
        summaryJson.Should().NotBeEmpty();
    }
}