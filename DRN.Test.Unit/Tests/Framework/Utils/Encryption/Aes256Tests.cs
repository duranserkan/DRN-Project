using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using DRN.Framework.Utils.Data.Encryption;

namespace DRN.Test.Unit.Tests.Framework.Utils.Encryption;

public class Aes256Tests
{
    // NIST SP 800-38A, Appendix F.1.5 ECB-AES256.Encrypt, Blocks #1-#4:
    // https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-38a.pdf
    private const string NistKeyHex = "603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4";
    private const string NistBlock1PlaintextHex = "6BC1BEE22E409F96E93D7E117393172A";
    private const string NistBlock1CiphertextHex = "F3EED1BDB5D2A03C064B5A7E3DB181F8";
    private const string NistBlock2PlaintextHex = "AE2D8A571E03AC9C9EB76FAC45AF8E51";
    private const string NistBlock2CiphertextHex = "591CCB10D410ED26DC5BA74A31362870";
    private const string NistBlock3PlaintextHex = "30C81C46A35CE411E5FBC1191A0A52EF";
    private const string NistBlock3CiphertextHex = "B6ED21B99CA6F4F9F153E7B1BEAFED1D";
    private const string NistBlock4PlaintextHex = "F69F2445DF4F9B17AD2B417BE66C3710";
    private const string NistBlock4CiphertextHex = "23304B7A39F9F3FF067D8D8F9E24ECC7";

    // IETF SKID Internet-Draft Appendix A: A.1 AES key derived with BLAKE3 derive-key mode from
    // sample Nexus key material 000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F;
    // A.3 plaintext; and A.4 ciphertext.
    // Nexus key material value is also used as the AES-256 test-vector KEK in RFC 3394 Section 4.3.
    // Source: DRN.Test.Unit/Tests/Framework/Utils/Ids/IetfTestVectorGeneratorTests.cs
    private const string IetfDraftKeyHex = "4988F97FF724CD086BDFEC83497C3527B3656F35F0911BEEAA6BCE4BB92D3BC7";
    private const string IetfDraftPlaintextHex = "00081B3200058D018D0C002A492C0E75";
    private const string IetfDraftCiphertextHex = "652068A43612CC4B8ABB83B853DC6786";

    [Theory]
    [DataInlineUnit("NIST SP 800-38A F.1.5 block #1", NistKeyHex, NistBlock1PlaintextHex, NistBlock1CiphertextHex)]
    [DataInlineUnit("NIST SP 800-38A F.1.5 block #2", NistKeyHex, NistBlock2PlaintextHex, NistBlock2CiphertextHex)]
    [DataInlineUnit("NIST SP 800-38A F.1.5 block #3", NistKeyHex, NistBlock3PlaintextHex, NistBlock3CiphertextHex)]
    [DataInlineUnit("NIST SP 800-38A F.1.5 block #4", NistKeyHex, NistBlock4PlaintextHex, NistBlock4CiphertextHex)]
    [DataInlineUnit("IETF SKID draft Appendix A", IetfDraftKeyHex, IetfDraftPlaintextHex, IetfDraftCiphertextHex)]
    public void Aes256_All_Implementations_Should_Match_Known_Answers(
        string vectorName,
        string keyHex,
        string plaintextHex,
        string ciphertextHex)
    {
        using var aes = new Aes256(Convert.FromHexString(keyHex));
        var plaintext = VectorFromHex(plaintextHex);
        var ciphertext = VectorFromHex(ciphertextHex);

        aes.Encrypt(plaintext).Should().Be(ciphertext, $"{vectorName} encryption");
        aes.Decrypt(ciphertext).Should().Be(plaintext, $"{vectorName} decryption");
        var frameworkCiphertext = aes.EncryptWithFramework(plaintext);
        frameworkCiphertext.Should().Be(ciphertext, $"{vectorName} framework encryption");
        aes.DecryptWithFramework(ciphertext).Should().Be(plaintext, $"{vectorName} framework decryption");

        if (!Aes256.IsSupported)
            return;

        var intrinsicCiphertext = aes.EncryptRuntimeIntrinsics(plaintext);
        intrinsicCiphertext.Should().Be(ciphertext, $"{vectorName} intrinsic encryption");
        intrinsicCiphertext.Should().Be(frameworkCiphertext, $"{vectorName} implementation compatibility");
        aes.DecryptRuntimeIntrinsics(ciphertext).Should().Be(plaintext, $"{vectorName} intrinsic decryption");
        aes.DecryptWithFramework(intrinsicCiphertext).Should().Be(plaintext, $"{vectorName} framework decryption of intrinsic ciphertext");
        aes.DecryptRuntimeIntrinsics(frameworkCiphertext).Should().Be(plaintext, $"{vectorName} intrinsic decryption of framework ciphertext");
    }

    [Fact]
    public void Aes256_Should_Support_Parallel_Use_Of_One_Instance()
    {
        using var aes = new Aes256(Convert.FromHexString(NistKeyHex));
        var plaintext = VectorFromHex(NistBlock1PlaintextHex);
        var ciphertext = VectorFromHex(NistBlock1CiphertextHex);
        var runtimeIntrinsicsSupported = Aes256.IsSupported;
        var failureCount = 0;

        Parallel.For(0, 100_000, _ =>
        {
            var frameworkEncrypted = aes.EncryptWithFramework(plaintext);
            var frameworkDecrypted = aes.DecryptWithFramework(frameworkEncrypted);

            if (!frameworkEncrypted.Equals(ciphertext) || !frameworkDecrypted.Equals(plaintext))
            {
                Interlocked.Increment(ref failureCount);
                return;
            }

            if (!runtimeIntrinsicsSupported)
                return;

            var intrinsicEncrypted = aes.EncryptRuntimeIntrinsics(plaintext);
            var intrinsicDecrypted = aes.DecryptRuntimeIntrinsics(intrinsicEncrypted);
            var frameworkDecryptedIntrinsicCiphertext = aes.DecryptWithFramework(intrinsicEncrypted);
            var intrinsicDecryptedFrameworkCiphertext = aes.DecryptRuntimeIntrinsics(frameworkEncrypted);

            if (!intrinsicEncrypted.Equals(frameworkEncrypted) ||
                !intrinsicEncrypted.Equals(ciphertext) ||
                !intrinsicDecrypted.Equals(plaintext) ||
                !frameworkDecryptedIntrinsicCiphertext.Equals(plaintext) ||
                !intrinsicDecryptedFrameworkCiphertext.Equals(plaintext))
                Interlocked.Increment(ref failureCount);
        });

        failureCount.Should().Be(0);
    }

    [Fact]
    public void Aes256_Implementations_Should_Be_Compatible_For_100_Random_Values()
    {
        for (var index = 0; index < 100; index++)
        {
            var key = RandomNumberGenerator.GetBytes(32);
            try
            {
                using var aes = new Aes256(key);
                var plaintext = VectorFromBytes(RandomNumberGenerator.GetBytes(16));
                var frameworkCiphertext = aes.EncryptWithFramework(plaintext);
                var automaticCiphertext = aes.Encrypt(plaintext);

                automaticCiphertext.Should().Be(frameworkCiphertext);
                aes.DecryptWithFramework(frameworkCiphertext).Should().Be(plaintext);
                aes.Decrypt(automaticCiphertext).Should().Be(plaintext);
                aes.DecryptWithFramework(automaticCiphertext).Should().Be(plaintext);
                aes.Decrypt(frameworkCiphertext).Should().Be(plaintext);

                if (!Aes256.IsSupported)
                    continue;

                var intrinsicCiphertext = aes.EncryptRuntimeIntrinsics(plaintext);
                intrinsicCiphertext.Should().Be(frameworkCiphertext);
                aes.DecryptRuntimeIntrinsics(intrinsicCiphertext).Should().Be(plaintext);
                aes.DecryptWithFramework(intrinsicCiphertext).Should().Be(plaintext);
                aes.Decrypt(intrinsicCiphertext).Should().Be(plaintext);
                aes.DecryptRuntimeIntrinsics(frameworkCiphertext).Should().Be(plaintext);
                aes.DecryptRuntimeIntrinsics(automaticCiphertext).Should().Be(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    [Fact]
    public void RuntimeIntrinsic_Methods_Should_Reject_Unsupported_Platforms()
    {
        if (Aes256.IsSupported)
            return;

        using var aes = new Aes256(Convert.FromHexString(NistKeyHex));

        Action encrypt = () => _ = aes.EncryptRuntimeIntrinsics(Vector128<byte>.Zero);
        Action decrypt = () => _ = aes.DecryptRuntimeIntrinsics(Vector128<byte>.Zero);

        encrypt.Should().Throw<PlatformNotSupportedException>();
        decrypt.Should().Throw<PlatformNotSupportedException>();
    }

    [Theory]
    [DataInlineUnit(0)]
    [DataInlineUnit(16)]
    [DataInlineUnit(24)]
    [DataInlineUnit(31)]
    [DataInlineUnit(33)]
    public void Aes256_Should_Reject_Non_256_Bit_Keys(int keyLength)
    {
        Action action = () => _ = new Aes256(new byte[keyLength]);

        action.Should().Throw<ArgumentException>().WithParameterName("key");
    }

    [Fact]
    public void Aes256_Should_Reject_All_Operations_After_Dispose()
    {
        var aes = new Aes256(Convert.FromHexString(NistKeyHex));
        aes.Dispose();
        aes.Dispose();

        Action[] operations =
        [
            () => _ = aes.Encrypt(Vector128<byte>.Zero),
            () => _ = aes.Decrypt(Vector128<byte>.Zero),
            () => _ = aes.EncryptWithFramework(Vector128<byte>.Zero),
            () => _ = aes.DecryptWithFramework(Vector128<byte>.Zero),
            () => _ = aes.EncryptRuntimeIntrinsics(Vector128<byte>.Zero),
            () => _ = aes.DecryptRuntimeIntrinsics(Vector128<byte>.Zero)
        ];

        foreach (var operation in operations)
            operation.Should().Throw<ObjectDisposedException>();
    }

    private static Vector128<byte> VectorFromHex(string value)
    {
        var bytes = Convert.FromHexString(value);
        return VectorFromBytes(bytes);
    }

    private static Vector128<byte> VectorFromBytes(byte[] value)
        => Vector128.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(value));
}
