using System.Security.Cryptography;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Data.Encryption;

internal sealed class NexusKeyMaterial(NexusMacKey macKey) : IDisposable
{
    // KeyAsBinary is used only for BLAKE3 keyed MAC integrity checks.
    public BinaryData MacKey { get; } = macKey.KeyAsBinary;

    // AlternativeKeyAsBinary is a derived, separate 32-byte key used only for AES-256 encryption.
    // AES-256 retains 128-bit security under Grover's algorithm and is suitable for post-quantum symmetric strength.
    public Aes Aes { get; } = CreateAes(macKey.AlternativeKeyAsBinary);

    public void Dispose() => Aes.Dispose();

    private static Aes CreateAes(BinaryData key)
    {
        var keyBytes = key.ToArray();
        if (keyBytes.Length != 32)
            throw new ArgumentException(
                $"AES-256-ECB requires a 32-byte key but received {keyBytes.Length} bytes. " +
                $"Verify that AlternativeKeyAsBinary produces a 256-bit key.",
                nameof(key));

        var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = keyBytes;

        return aes;
    }
}
