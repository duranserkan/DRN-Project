using System.Security.Cryptography;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Data.Encryption;

internal sealed class NexusKeyMaterial(NexusKey key) : IDisposable
{
    // MacKey is BLAKE3-derived material used only for keyed MAC integrity checks.
    public BinaryData MacKey { get; } = key.MacKey;

    // EncryptionKey is separate BLAKE3-derived 32-byte material used only for AES-256 encryption.
    // AES-256 retains 128-bit security under Grover's algorithm and is suitable for post-quantum symmetric strength.
    public Aes Aes { get; } = CreateAes(key.EncryptionKey);

    public void Dispose() => Aes.Dispose();

    private static Aes CreateAes(BinaryData key)
    {
        if (key.Length != 32)
            throw new ArgumentException(
                $"AES-256-ECB requires a 32-byte key but received {key.Length} bytes. Verify that EncryptionKey produces a 256-bit key.",
                nameof(key));

        var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key.ToArray();

        return aes;
    }
}
