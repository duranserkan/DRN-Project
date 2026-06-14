using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Data.Encodings;

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
    public void AppSettings_Should_Generate_Base64Url_MacKey_In_Development_When_Default_Not_Configured()
    {
        byte appId = 56;
        byte appInstanceId = 21;

        var settings = AppSettings.Development(GetCustomSettings(appId, appInstanceId));
        var defaultMacKey = settings.NexusAppSettings.GetDefaultMacKey();

        defaultMacKey.Default.Should().BeTrue();
        defaultMacKey.Format.Should().Be(ByteEncoding.Base64UrlEncoded);
        defaultMacKey.IsValid.Should().BeTrue();
        defaultMacKey.Key.Decode(ByteEncoding.Base64UrlEncoded).Length.Should().Be(32);
    }

    [Fact]
    public void AppSettings_Should_Throw_Configuration_Exception_When_Default_MacKey_Missing_Outside_Development()
    {
        var configuration = new ConfigurationManager()
            .AddObjectToJsonConfiguration(new
            {
                Environment = "Staging",
                NexusAppSettings = new NexusAppSettings { AppId = 1, AppInstanceId = 1 }
            })
            .Build();

        var action = () => new AppSettings(configuration);

        action.Should().ThrowExactly<ConfigurationException>();
    }

    [Fact]
    public void AppSettings_Should_Thrown_Configuration_Exception_For_Invalid_NexusAppId()
    {
        byte appId = 128;
        byte appInstanceId = 21;

        var custom = GetCustomSettings(appId, appInstanceId);
        var action = () => AppSettings.Development(custom);
        action.Should().ThrowExactly<ConfigurationException>();
    }

    [Fact]
    public void AppSettings_Should_Thrown_Configuration_Exception_For_Invalid_NexusAppInstanceId()
    {
        byte appId = 61;
        byte appInstanceId = 64;

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
