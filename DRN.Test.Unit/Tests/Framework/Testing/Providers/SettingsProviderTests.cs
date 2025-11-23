namespace DRN.Test.Unit.Tests.Framework.Testing.Providers;

public class SettingsProviderTests
{
    [Fact]
    public void SettingsProvider_Should_Return_IAppSettings_Instance()
    {
        var appSettings = SettingsProvider.GetAppSettings();

        appSettings.GetRequiredSection("AllowedHosts").Value.Should().Be("*");
        appSettings.TryGetSection("Bar", out _).Should().BeTrue();
        appSettings.TryGetSection("Foo", out _).Should().BeFalse();
        appSettings.GetRequiredConnectionString("Foo").Should().Be("Bar");
        appSettings.TryGetConnectionString("Bar", out _).Should().BeFalse();
    }

    [Fact]
    public void SettingsProvider_Should_Return_IConfiguration_Instance()
    {
        var configuration = SettingsProvider.GetConfiguration("alternateSettings");

        configuration.GetRequiredSection("AllowedHosts").Value.Should().Be("*");
        configuration.GetSection("Foo").Exists().Should().BeTrue();
        configuration.GetSection("Bar").Exists().Should().BeFalse();
        configuration.GetConnectionString("Bar").Should().Be("Foo");
    }

    [Theory]
    [DataInlineUnit("settings", "localhost")]
    [DataInlineUnit("alternateSettings", "127.0. 0.1")]
    [DataInlineUnit("globalDummySettings", "*")]
    public void SettingsProvider_Should_Return_Test_Specific_IConfiguration_Instance(DrnTestContextUnit context, string settingsName, string value)
    {
        var settingsPath = context.GetSettingsPath(settingsName);
        settingsPath.DataExists.Should().BeTrue();
        var settingsData = context.GetSettingsData(settingsName);
        settingsData.DataExists.Should().BeTrue();

        var configuration = SettingsProvider.GetConfiguration(settingsName, context.MethodContext.GetTestFolderLocation());
        configuration.GetRequiredSection("AllowedHosts").Value.Should().Be(value);

        context.BuildServiceProvider(settingsName);
        context.GetRequiredService<IConfiguration>().GetRequiredSection("AllowedHosts").Value.Should().Be(value);
    }

    [Theory]
    [DataInlineUnit("settings", "localhost")]
    [DataInlineUnit("alternateSettings", "127.0. 0.1")]
    public void SettingsProvider_Should_Return_Test_Specific_IAppSettings_Instance(DrnTestContextUnit context, string settingsName, string value)
    {
        var appSettings = SettingsProvider.GetAppSettings(settingsName, context.MethodContext.GetTestFolderLocation());
        appSettings.GetRequiredSection("AllowedHosts").Value.Should().Be(value);

        context.BuildServiceProvider(settingsName);
        context.GetRequiredService<IAppSettings>().GetRequiredSection("AllowedHosts").Value.Should().Be(value);
    }

    [Theory]
    [DataInlineUnit("localhost")]
    public void DrnTestContext_Should_Add_Settings_Json_To_Configuration(DrnTestContextUnit context, string value)
    {
        //settings.json file can be found in the same folder with test file, in the global Settings folder or Settings folder that stays in the same folder with test file
        context.GetRequiredService<IAppSettings>().GetRequiredSection("AllowedHosts").Value.Should().Be(value);
    }
}