using System.Security.Cryptography;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Data.Encryption;

internal sealed class NexusSecret : IDisposable
{
    public NexusSecret(NexusKey key)
    {
        MacKey = new SecretKey32(key.MacKey.Span);
        try
        {
            Aes = CreateAes(key.EncryptionKey);
        }
        catch
        {
            MacKey.Dispose();
            throw;
        }
    }

    // MacKey is BLAKE3-derived material used only for keyed MAC integrity checks.
    internal SecretKey32 MacKey { get; }

    // EncryptionKey is separate BLAKE3-derived 32-byte material used only for AES-256 encryption.
    // AES-256 retains 128-bit security under Grover's algorithm and is suitable for post-quantum symmetric strength.
    internal Aes Aes { get; }

    public void Dispose()
    {
        MacKey.Dispose();
        Aes.Dispose();
    }

    private static Aes CreateAes(SecretKey32 key)
    {
        if (key.Length != 32)
            throw new ArgumentException(
                $"AES-256-ECB requires a 32-byte key but received {key.Length} bytes. Verify that EncryptionKey produces a 256-bit key.",
                nameof(key));

        var aes = Aes.Create();
        try
        {
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = key.Bytes;
        }
        catch
        {
            aes.Dispose();
            throw;
        }

        return aes;
    }
}
