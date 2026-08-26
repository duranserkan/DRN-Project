using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Blake3;
using DRN.Framework.Utils.Data.Encodings;

namespace DRN.Test.Unit.Tests.Framework.Utils.Settings;

public class AppSecuritySettingsTests
{
    private const string AppHashKeyDerivationContext =
        "DRN.Framework.Utils AppSecuritySettings Peace at home AppHashKey 2026-06-29 21:57:43 v1";
    private const string AppEncryptionKeyDerivationContext =
        "DRN.Framework.Utils AppSecuritySettings Peace in the world AppEncryptionKey 2026-06-29 21:57:43 v1";
    private const string AppKeyDerivationContext =
        "DRN.Framework.Utils AppSecuritySettings 1919 MKA 1923 AppKey 2026-06-29 21:57:43 v1";
    private const string AppSeedDerivationContext =
        "DRN.Framework.Utils AppSecuritySettings 1923 DRN 2923 AppSeed 2026-06-29 21:57:43 v1";

    [Theory]
    [DataInlineUnit]
    public void SecuritySettings_Should_BeValid(DrnTestContextUnit drnTestContext)
    {
        var securitySettings = drnTestContext.GetRequiredService<IAppSecuritySettings>();

        var decodedEncryptionKey = securitySettings.AppEncryptionKey.Decode();
        decodedEncryptionKey.Length.Should().Be(32);

        var decodedHashKey = securitySettings.AppHashKey.Decode();
        decodedHashKey.Length.Should().Be(32);

        var decodedAppKey = securitySettings.AppKey;
        decodedAppKey.Length.Should().Be(8);
    }

    [Fact]
    public void SecuritySettings_Should_Derive_Values_With_Blake3_DeriveKey_Mode()
    {
        var features = new DrnAppFeatures();
        var securitySettings = new AppSecuritySettings(features);

        securitySettings.AppHashKey.Should().Be(DeriveBase64UrlKey(features.SeedKey, AppHashKeyDerivationContext));
        securitySettings.AppEncryptionKey.Should().Be(DeriveBase64UrlKey(features.SeedKey, AppEncryptionKeyDerivationContext));
        securitySettings.AppKey.Should().Be(DeriveBase64UrlKey(features.SeedKey, AppKeyDerivationContext)[..8]);
        securitySettings.AppSeed.Should().Be(DeriveSeed(features.SeedKey));
    }

    [Fact]
    public void SecuritySettings_Should_Support_Internal_Default_Constructor_For_Testing()
    {
        var defaultFeatures = new DrnAppFeatures();
        var securitySettingsDefault = new AppSecuritySettings();

        securitySettingsDefault.AppHashKey.Should().Be(DeriveBase64UrlKey(defaultFeatures.SeedKey, AppHashKeyDerivationContext));
        securitySettingsDefault.AppEncryptionKey.Should().Be(DeriveBase64UrlKey(defaultFeatures.SeedKey, AppEncryptionKeyDerivationContext));
        securitySettingsDefault.AppKey.Should().Be(DeriveBase64UrlKey(defaultFeatures.SeedKey, AppKeyDerivationContext)[..8]);
        securitySettingsDefault.AppSeed.Should().Be(DeriveSeed(defaultFeatures.SeedKey));
    }

    [Fact]
    public void SecuritySettings_Should_Throw_When_Features_Is_Null()
    {
        var act = () => new AppSecuritySettings(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [DataInlineUnit(null!)]
    [DataInlineUnit("")]
    [DataInlineUnit("   ")]
    public void SecuritySettings_Should_Throw_When_Supplied_SeedKey_Is_NullOrWhiteSpace(string? invalidSeedKey)
    {
        var features = new DrnAppFeatures { SeedKey = invalidSeedKey! };
        var act = () => new AppSecuritySettings(features);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Default_Interface_CreateAesGcm_Should_Succeed_For_Custom_Implementations()
    {
        IAppSecuritySettings customSettings = new CustomSecuritySettings();
        using var aesGcm = customSettings.CreateAesGcm("CustomContext");
        aesGcm.Should().NotBeNull();
    }

    [Fact]
    public void CreateAesGcm_Should_Initialize_AesGcm_With_Context_Derivation()
    {
        var features = new DrnAppFeatures { SeedKey = "TestSeedForAesGcmCreation_1234567890" };
        var securitySettings = new AppSecuritySettings(features);

        using var aesGcm = securitySettings.CreateAesGcm("TestAesGcmContext v1");
        aesGcm.Should().NotBeNull();

        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        var plaintext = Encoding.UTF8.GetBytes("HelloWorldDataProtection");
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        var decrypted = new byte[plaintext.Length];
        aesGcm.Decrypt(nonce, ciphertext, tag, decrypted);

        Encoding.UTF8.GetString(decrypted).Should().Be("HelloWorldDataProtection");
    }

    [Theory]
    [DataInlineUnit(null!)]
    [DataInlineUnit("")]
    [DataInlineUnit("   ")]
    public void CreateAesGcm_Should_Throw_On_NullOrWhiteSpace_Context(string? invalidContext)
    {
        var securitySettings = new AppSecuritySettings(new DrnAppFeatures());

        var act = () => securitySettings.CreateAesGcm(invalidContext!);
        act.Should().Throw<ArgumentException>();
    }

    private static string DeriveBase64UrlKey(string seedKey, string context)
        => Derive32ByteKey(seedKey, context).Encode(ByteEncoding.Base64UrlEncoded);

    private static long DeriveSeed(string seedKey)
        => BinaryPrimitives.ReadInt64LittleEndian(Derive32ByteKey(seedKey, AppSeedDerivationContext));

    private static byte[] Derive32ByteKey(string seedKey, string context)
    {
        Span<byte> derived = stackalloc byte[32];
        using var hasher = Hasher.NewDeriveKey(context);
        hasher.Update(Encoding.UTF8.GetBytes(seedKey));
        hasher.Finalize(derived);

        return derived.ToArray();
    }

    private sealed class CustomSecuritySettings : IAppSecuritySettings
    {
        public string AppKey => "12345678";
        public string AppHashKey => "AppHashKeyMaterial32BytesLength12";
        public string AppEncryptionKey => "AppEncryptionKey32BytesLength123";
        public long AppSeed => 123456789L;
    }
}
