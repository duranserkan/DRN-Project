using System.Security.Cryptography;

namespace DRN.Framework.Utils.Data.Encryption;

/// <summary>
/// Owns fixed-size 32-byte secret key material.
/// </summary>
/// <remarks>
/// The input is copied on construction. Internal accessors return read-only views or the owned buffer, and disposal clears the owned array.
/// </remarks>
internal sealed class SecretKey32 : IDisposable
{
    internal const int KeyLength = 32;

    private byte[]? _bytes;

    internal SecretKey32(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != KeyLength)
            throw new ArgumentException($"Secret key material must be exactly {KeyLength} bytes; received {bytes.Length}.", nameof(bytes));

        _bytes = bytes.ToArray();
    }

    internal int Length => Bytes.Length;

    internal ReadOnlySpan<byte> Span => Bytes;

    internal ReadOnlyMemory<byte> Memory => Bytes;
    
    internal byte[] Bytes => _bytes ?? throw new ObjectDisposedException(nameof(SecretKey32));

    ~SecretKey32() => ZeroAndRelease();
    
    public void Dispose()
    {
        ZeroAndRelease();
        GC.SuppressFinalize(this);
    }

    private void ZeroAndRelease()
    {
        var bytes = Interlocked.Exchange(ref _bytes, null);
        if (bytes is null)
            return;

        CryptographicOperations.ZeroMemory(bytes);
    }
}
