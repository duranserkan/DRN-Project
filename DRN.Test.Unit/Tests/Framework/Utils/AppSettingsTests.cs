using System.Text;
using Blake3;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Data.Encodings;

namespace DRN.Test.Unit.Tests.Framework.Utils;

public class AppSettingsTests
{
    [Theory]
    [DataInlineUnit(null, AppEnvironment.Development)]
    [DataInlineUnit("", AppEnvironment.Development)]
    [DataInlineUnit(" ", AppEnvironment.Development)]
    [DataInlineUnit(null, AppEnvironment.Staging)]
    [DataInlineUnit(null, AppEnvironment.Production)]
    [DataInlineUnit(null, AppEnvironment.NotDefined)]
    public void AppId_Must_Be_Explicitly_Configured(string? appId, AppEnvironment environment)
    {
        var configuration = new ConfigurationManager().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Environment"] = environment.ToString(),
            ["NexusAppSettings:AppId"] = appId,
            ["NexusAppSettings:AppInstanceId"] = "0"
        });

        var create = () => new AppSettings(configuration.Build());
        create.Should().Throw<ConfigurationException>().WithMessage("*AppId must be explicitly configured*");
        var omittedConfiguration = new ConfigurationBuilder()
            .AddObjectToJsonConfiguration(new { Environment = environment.ToString(), NexusAppSettings = new { AppInstanceId = 0 } }).Build();
        var createWithoutAppId = () => new AppSettings(omittedConfiguration);
        createWithoutAppId.Should().Throw<ConfigurationException>().WithMessage("*AppId must be explicitly configured*");
    }

    [Fact]
    public void Explicit_Default_Identifiers_Zero_Are_Valid()
    {
        var configuration = new ConfigurationBuilder().AddObjectToJsonConfiguration(new
        {
            Environment = "Development", NexusAppSettings = new { AppId = 0, AppInstanceId = 0 }
        }).Build();
        using var settings = new AppSettings(configuration);
        settings.NexusAppSettings.AppId.Should().Be(0);
        settings.NexusAppSettings.AppInstanceId.Should().Be(0);
    }

    [Theory]
    [DataInlineUnit(AppEnvironment.Development)]
    [DataInlineUnit(AppEnvironment.Staging)]
    [DataInlineUnit(AppEnvironment.Production)]
    [DataInlineUnit(AppEnvironment.NotDefined)]
    public void AppInstanceId_Must_Be_Explicitly_Configured(AppEnvironment environment)
    {
        foreach (var value in new string?[] { null, "", " " })
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Environment"] = environment.ToString(),
                ["NexusAppSettings:AppId"] = "0",
                ["NexusAppSettings:AppInstanceId"] = value
            }).Build();
            var create = () => new AppSettings(configuration);
            create.Should().Throw<ConfigurationException>().WithMessage("*AppInstanceId must be explicitly configured*");
        }

        var omittedConfiguration = new ConfigurationBuilder().AddObjectToJsonConfiguration(new
        {
            Environment = environment.ToString(), NexusAppSettings = new { AppId = 0 }
        }).Build();
        var createWithoutInstanceId = () => new AppSettings(omittedConfiguration);
        createWithoutInstanceId.Should().Throw<ConfigurationException>().WithMessage("*AppInstanceId must be explicitly configured*");
    }

    private const string DevelopmentNexusKeyMaterialDerivationContext =
        "DRN.Framework.Utils Development NexusKey material from 1881 to 193∞ Forever 2026-06-29 21:57:43 v1";

    [Fact]
    public void AppSettings_Should_Create_Development_Settings_With_Derived_Default_NexusKey()
    {
        byte appId = 56;
        byte appInstanceId = 21;

        var custom = GetCustomSettings(appId, appInstanceId);
        using var settings = new AppSettings(new ConfigurationBuilder()
            .AddObjectToJsonConfiguration(new { Environment = "Development" })
            .AddObjectToJsonConfiguration(custom).Build());

        settings.Environment.Should().Be(AppEnvironment.Development);
        settings.NexusAppSettings.AppId.Should().Be(appId);
        settings.NexusAppSettings.AppInstanceId.Should().Be(appInstanceId);
        var defaultNexusKey = settings.NexusAppSettings.GetDefaultKey();

        defaultNexusKey.Default.Should().BeTrue();
        defaultNexusKey.Format.Should().Be(ByteEncoding.Base64UrlEncoded);
        defaultNexusKey.KeyMaterial.Decode(encoding: ByteEncoding.Base64UrlEncoded).Length.Should().Be(32);
        var securitySettings = new AppSecuritySettings(settings.Features);

        defaultNexusKey.KeyMaterial.Should().Be(DeriveExpectedDevelopmentNexusKeyMaterial(securitySettings));
    }

    [Fact]
    public void AppSettings_Should_Dispose_NexusAppSettings_Key_Material()
    {
        var settings = new AppSettings(new ConfigurationBuilder()
            .AddObjectToJsonConfiguration(new { Environment = "Development" })
            .AddObjectToJsonConfiguration(GetCustomSettings(56, 21)).Build());
        var defaultNexusKey = settings.NexusAppSettings.GetDefaultKey();

        settings.Dispose();

        var action = () => { _ = defaultNexusKey.MacKey.Bytes; };
        action.Should().Throw<ObjectDisposedException>();
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
                DrnAppFeatures = new
                {
                    SeedKey = "Our true mentor in life is science! - Mustafa Kemal Atatürk (1924)"
                },
                NexusAppSettings = new NexusAppSettings { AppId = 1, AppInstanceId = 1 }
            })
            .Build();

        var action = () => new AppSettings(configuration);

        var exception = action.Should().ThrowExactly<ConfigurationException>().Which;
        exception.Message.Should().Contain("Default Nexus key not found for the environment: Staging");
    }

    [Theory]
    [DataInlineUnit(AppEnvironment.Staging)]
    [DataInlineUnit(AppEnvironment.Production)]
    [DataInlineUnit(AppEnvironment.NotDefined)]
    public void AppSettings_Should_Throw_Configuration_Exception_When_Default_SeedKey_Used_Outside_Development(AppEnvironment environment)
    {
        var configuration = new ConfigurationManager()
            .AddObjectToJsonConfiguration(new
            {
                Environment = environment.ToString(),
                NexusAppSettings = new NexusAppSettings
                {
                    AppId = 1,
                    AppInstanceId = 1,
                    Keys = [new NexusKey(new string('A', 32)) { Default = true }]
                }
            })
            .Build();

        var action = () => new AppSettings(configuration);

        var exception = action.Should().ThrowExactly<ConfigurationException>().Which;
        exception.Message.Should().Contain($"Default DrnAppFeatures.SeedKey is not allowed for the environment: {environment}");
    }

    [Theory]
    [DataInlineUnit(AppEnvironment.Development)]
    [DataInlineUnit(AppEnvironment.Staging)]
    [DataInlineUnit(AppEnvironment.Production)]
    [DataInlineUnit(AppEnvironment.NotDefined)]
    public void AppSettings_Should_Throw_Configuration_Exception_When_Sample_SeedKey_Used_Outside_Tests(AppEnvironment environment)
    {
        using var _ = TestEnvironment.SetTestContextEnabledScope(false);

        var configuration = new ConfigurationManager()
            .AddObjectToJsonConfiguration(new
            {
                Environment = environment.ToString(),
                DrnAppFeatures = new
                {
                    SeedKey = DrnAppFeatures.SampleSeedKey
                },
                NexusAppSettings = new NexusAppSettings
                {
                    AppId = 1,
                    AppInstanceId = 1,
                    Keys = [new NexusKey(new string('A', 32)) { Default = true }]
                }
            })
            .Build();

        var action = () => new AppSettings(configuration);

        var exception = action.Should().ThrowExactly<ConfigurationException>().Which;
        exception.Message.Should().Contain("Sample DrnAppFeatures.SeedKey is not allowed outside test execution");
    }

    [Fact]
    public void NexusAppSettings_HasDefaultKey_Should_Report_Null_NexusKey_From_Validation()
    {
        var nexusAppSettings = new NexusAppSettings
        {
            Keys = [null!]
        };

        Func<bool> action = nexusAppSettings.HasDefaultKey;

        var exception = action.Should().ThrowExactly<ConfigurationException>().Which;
        exception.Message.Should().Be("NexusAppSettings.Keys[0] must not be null");
    }

    [Fact]
    public void AppSettings_Should_Derive_Development_NexusKey_When_Configured_Keys_Have_No_Default()
    {
        var configuredKey = new NexusKey(new string('A', 32));

        using var settings = new AppSettings(new ConfigurationBuilder().AddObjectToJsonConfiguration(new
        {
            Environment = "Development",
            NexusAppSettings = new NexusAppSettings
            {
                AppId = 1,
                AppInstanceId = 1,
                Keys = [configuredKey]
            }
        }).Build());

        var defaultKey = settings.NexusAppSettings.GetDefaultKey();
        defaultKey.Default.Should().BeTrue();
        defaultKey.Format.Should().Be(ByteEncoding.Base64UrlEncoded);
        settings.NexusAppSettings.Keys.Should().Contain(key => key.KeyMaterial == configuredKey.KeyMaterial && !key.Default);
    }

    [Theory]
    [DataInlineUnit((byte)128, (byte)21)]
    [DataInlineUnit((byte)61, (byte)64)]
    public void AppSettings_Should_Throw_Validation_Exception_For_Invalid_Nexus_Identifiers(byte appId, byte appInstanceId)
    {
        var custom = GetCustomSettings(appId, appInstanceId);
        var configuration = new ConfigurationBuilder()
            .AddObjectToJsonConfiguration(new { Environment = "Development" })
            .AddObjectToJsonConfiguration(custom).Build();
        var action = () => new AppSettings(configuration);
        action.Should().ThrowExactly<ValidationException>();
    }

    private static object GetCustomSettings(byte appId, byte appInstanceId)
    {
        var custom = new { NexusAppSettings = new NexusAppSettings { AppId = appId, AppInstanceId = appInstanceId, MacType = NexusMacType.Blake3 } };
        return custom;
    }

    private static string DeriveExpectedDevelopmentNexusKeyMaterial(AppSecuritySettings securitySettings)
    {
        Span<byte> derived = stackalloc byte[32];
        using var hasher = Hasher.NewDeriveKey(DevelopmentNexusKeyMaterialDerivationContext);
        hasher.Update(Encoding.UTF8.GetBytes($"{securitySettings.AppHashKey}:{securitySettings.AppEncryptionKey}:{securitySettings.AppKey}"));
        hasher.Finalize(derived);

        return derived.Encode(encoding: ByteEncoding.Base64UrlEncoded);
    }
}
