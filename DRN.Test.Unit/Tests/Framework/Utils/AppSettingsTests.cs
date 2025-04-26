using DRN.Framework.SharedKernel.Enums;

namespace DRN.Test.Unit.Tests.Framework.Utils;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_Should_Be_Obtained()
    {
        byte appId = 56;
        byte appInstanceId = 21;

        var custom = GetCustomSettings(appId, appInstanceId);
        var settings = AppSettings.Development(custom);

        settings.Environment.Should().Be(AppEnvironment.Development);
        settings.NexusAppSettings.AppId.Should().Be(appId);
        settings.NexusAppSettings.AppInstanceId.Should().Be(appInstanceId);
    }

    [Fact]
    public void AppSettings_Should_Thrown_Configuration_Exception_For_Invalid_NexusAppId()
    {
        byte appId = 64;
        byte appInstanceId = 21;

        var custom = GetCustomSettings(appId, appInstanceId);
        var action = () => AppSettings.Development(custom);
        action.Should().ThrowExactly<ConfigurationException>();
    }

    [Fact]
    public void AppSettings_Should_Thrown_Configuration_Exception_For_Invalid_NexusAppInstanceId()
    {
        byte appId = 61;
        byte appInstanceId = 32;

        var custom = GetCustomSettings(appId, appInstanceId);
        var action = () => AppSettings.Development(custom);
        action.Should().ThrowExactly<ConfigurationException>();
    }
    
    private static object GetCustomSettings(byte appId, byte appInstanceId)
    {
        var custom = new { NexusAppSettings = new NexusAppSettings { AppId = appId, AppInstanceId = appInstanceId } };
        return custom;
    }
}