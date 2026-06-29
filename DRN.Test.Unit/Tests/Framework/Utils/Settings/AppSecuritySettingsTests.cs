using System.Buffers.Binary;
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
}
