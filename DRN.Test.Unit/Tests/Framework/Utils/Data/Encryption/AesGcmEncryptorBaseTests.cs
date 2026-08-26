using System.Security.Cryptography;
using System.Text;
using DRN.Framework.Utils.Data.Encryption;
using DRN.Framework.Utils.Settings;

namespace DRN.Test.Unit.Tests.Framework.Utils.Data.Encryption;

public class AesGcmEncryptorBaseTests
{
    private sealed class TestAesGcmEncryptor(IAppSecuritySettings securitySettings) : AesGcmEncryptorBase(securitySettings)
    {
        protected override string Context => "DRN.Test.Unit AesGcmEncryptorBaseTests 2026-08-26 v1";
    }

    [Fact]
    public void Encrypt_And_Decrypt_ByteArray_Should_RoundTrip()
    {
        var features = new DrnAppFeatures { SeedKey = "AesGcmEncryptorBaseTestSeed_1234567890" };
        var securitySettings = new AppSecuritySettings(features);
        using var encryptor = new TestAesGcmEncryptor(securitySettings);

        var originalText = "Sensitive Payload for Generic Encryption";
        var originalBytes = Encoding.UTF8.GetBytes(originalText);

        var encrypted = encryptor.Encrypt(originalBytes);

        encrypted.Ciphertext.Should().NotBeNull();
        encrypted.Ciphertext.Length.Should().Be(originalBytes.Length);
        encrypted.Nonce.Length.Should().Be(12);
        encrypted.Tag.Length.Should().Be(16);

        var decryptedBytes = encryptor.Decrypt(encrypted.Nonce, encrypted.Ciphertext, encrypted.Tag);
        Encoding.UTF8.GetString(decryptedBytes).Should().Be(originalText);
    }

    [Fact]
    public void Encrypt_And_Decrypt_Span_Overloads_Should_RoundTrip()
    {
        var features = new DrnAppFeatures { SeedKey = "AesGcmEncryptorBaseSpanTestSeed_1234567890" };
        var securitySettings = new AppSecuritySettings(features);
        using var encryptor = new TestAesGcmEncryptor(securitySettings);

        ReadOnlySpan<byte> plaintext = "SpanBasedDataEncryption"u8;
        Span<byte> ciphertext = stackalloc byte[plaintext.Length];
        Span<byte> nonce = stackalloc byte[12];
        Span<byte> tag = stackalloc byte[16];

        encryptor.Encrypt(plaintext, ciphertext, nonce, tag);

        Span<byte> decrypted = stackalloc byte[plaintext.Length];
        encryptor.Decrypt(nonce, ciphertext, tag, decrypted);

        decrypted.SequenceEqual(plaintext).Should().BeTrue();
    }

    [Fact]
    public void Decrypt_Should_Throw_On_Tampered_Tag()
    {
        var features = new DrnAppFeatures { SeedKey = "AesGcmEncryptorBaseTamperSeed_1234567890" };
        var securitySettings = new AppSecuritySettings(features);
        using var encryptor = new TestAesGcmEncryptor(securitySettings);

        var originalBytes = "DataToTamper"u8.ToArray();
        var encrypted = encryptor.Encrypt(originalBytes);

        // Corrupt tag
        encrypted.Tag[0] ^= 0xFF;

        var act = () => encryptor.Decrypt(encrypted.Nonce, encrypted.Ciphertext, encrypted.Tag);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Constructor_Should_Throw_On_Null_SecuritySettings()
    {
        var act = () => new TestAesGcmEncryptor(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
