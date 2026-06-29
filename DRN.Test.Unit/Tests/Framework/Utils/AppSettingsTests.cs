using System.Text;
using Blake3;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Data.Encodings;

namespace DRN.Test.Unit.Tests.Framework.Utils;

public class AppSettingsTests
{
    private const string DevelopmentNexusKeyMaterialDerivationContext =
        "DRN.Framework.Utils Development NexusKey material from 1881 to 193∞ Forever 2026-06-29 21:57:43 v1";

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
    public void AppSettings_Should_Generate_Base64Url_NexusKey_In_Development_When_Default_Not_Configured()
    {
        byte appId = 56;
        byte appInstanceId = 21;

        var settings = AppSettings.Development(GetCustomSettings(appId, appInstanceId));
        var defaultNexusKey = settings.NexusAppSettings.GetDefaultKey();

        defaultNexusKey.Default.Should().BeTrue();
        defaultNexusKey.Format.Should().Be(ByteEncoding.Base64UrlEncoded);
        defaultNexusKey.IsValid.Should().BeTrue();
        defaultNexusKey.KeyMaterial.Decode(ByteEncoding.Base64UrlEncoded).Length.Should().Be(32);
    }

    [Fact]
    public void AppSettings_Should_Derive_Development_NexusKey_Material_With_Blake3_DeriveKey_Mode()
    {
        var settings = AppSettings.Development(GetCustomSettings(56, 21));
        var securitySettings = new AppSecuritySettings(settings.Features);
        var defaultNexusKey = settings.NexusAppSettings.GetDefaultKey();

        defaultNexusKey.KeyMaterial.Should().Be(DeriveExpectedDevelopmentNexusKeyMaterial(securitySettings));
    }

    [Fact]
    public void AppSettings_Should_Reject_Legacy_MacKeys_Configuration_Before_Development_Key_Generation()
    {
        var configuration = new ConfigurationManager()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Environment"] = nameof(AppEnvironment.Development),
                ["NexusAppSettings:AppId"] = "1",
                ["NexusAppSettings:AppInstanceId"] = "1",
                ["NexusAppSettings:MacKeys:0:Key"] = new string('A', 32),
                ["NexusAppSettings:MacKeys:0:Format"] = nameof(ByteEncoding.Utf8),
                ["NexusAppSettings:MacKeys:0:Default"] = bool.TrueString
            })
            .Build();

        var action = () => new AppSettings(configuration);

        var exception = action.Should().ThrowExactly<ConfigurationException>().Which;
        exception.Message.Should().Contain("NexusAppSettings:MacKeys");
        exception.Message.Should().Contain("NexusAppSettings:Keys");
        exception.Message.Should().Contain("MacKeys[*].Key");
        exception.Message.Should().Contain("Keys[*].KeyMaterial");
    }

    [Fact]
    public void AppSettings_Should_Throw_Configuration_Exception_When_Default_NexusKey_Missing_Outside_Development()
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
    public void NexusAppSettings_HasDefaultKey_Should_Report_Null_NexusKey_From_Validation()
    {
        var nexusAppSettings = new NexusAppSettings
        {
            Keys = [null!]
        };

        var action = () => nexusAppSettings.HasDefaultKey();

        var exception = action.Should().ThrowExactly<ConfigurationException>().Which;
        exception.Message.Should().Be("NexusAppSettings.Keys[0] must not be null");
    }

    [Fact]
    public void AppSettings_Should_Derive_Development_NexusKey_When_Configured_Keys_Have_No_Default()
    {
        var configuredKey = new NexusKey(new string('A', 32));

        var settings = AppSettings.Development(new
        {
            NexusAppSettings = new NexusAppSettings
            {
                AppId = 1,
                AppInstanceId = 1,
                Keys = [configuredKey]
            }
        });

        var defaultKey = settings.NexusAppSettings.GetDefaultKey();
        defaultKey.Default.Should().BeTrue();
        defaultKey.Format.Should().Be(ByteEncoding.Base64UrlEncoded);
        settings.NexusAppSettings.Keys.Should().Contain(key => key.KeyMaterial == configuredKey.KeyMaterial && !key.Default);
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

    private static string DeriveExpectedDevelopmentNexusKeyMaterial(AppSecuritySettings securitySettings)
    {
        Span<byte> derived = stackalloc byte[32];
        using var hasher = Hasher.NewDeriveKey(DevelopmentNexusKeyMaterialDerivationContext);
        hasher.Update(Encoding.UTF8.GetBytes($"{securitySettings.AppHashKey}:{securitySettings.AppEncryptionKey}:{securitySettings.AppKey}"));
        hasher.Finalize(derived);

        return derived.Encode(ByteEncoding.Base64UrlEncoded);
    }
}
