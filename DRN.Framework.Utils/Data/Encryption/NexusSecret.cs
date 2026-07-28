using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Data.Encryption;

internal sealed class NexusSecret : IDisposable
{
    public NexusSecret(NexusKey key)
    {
        MacKey = new SecretKey32(key.MacKey.Span);
        try
        {
            Aes = new Aes256(key.EncryptionKey.Span);
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
    internal Aes256 Aes { get; }

    public void Dispose()
    {
        MacKey.Dispose();
        Aes.Dispose();
    }
}
