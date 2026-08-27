using System.Security.Cryptography;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Data.Encryption;

/// <summary>
/// Abstract base class for AES-256-GCM authenticated encryption and decryption.
/// Encapsulates cipher instantiation, context-separated BLAKE3 key derivation from <see cref="IAppSecuritySettings"/>,
/// and secure lifecycle management.
/// </summary>
public abstract class AesGcmEncryptorBase : IDisposable
{
    private readonly Lazy<AesGcm> _aesGcm;

    /// <summary>
    /// The domain separation context string used to derive a dedicated subkey for this cipher.
    /// </summary>
    protected abstract string Context { get; }

    protected AesGcmEncryptorBase(IAppSecuritySettings securitySettings)
    {
        ArgumentNullException.ThrowIfNull(securitySettings);
        _aesGcm = new Lazy<AesGcm>(() => securitySettings.CreateAesGcm(Context));
    }

    /// <summary>
    /// Gets the underlying initialized <see cref="AesGcm"/> cipher instance.
    /// </summary>
    protected AesGcm Cipher => _aesGcm.Value;

    /// <summary>
    /// Encrypts plaintext using AES-256-GCM with a newly generated random 96-bit nonce.
    /// </summary>
    public AesGcmEncryptedData Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var ciphertext = new byte[plaintext.Length];
        _aesGcm.Value.Encrypt(nonce, plaintext, ciphertext, tag);

        return new AesGcmEncryptedData(ciphertext, nonce, tag);
    }

    /// <summary>
    /// Encrypts plaintext into caller-provided spans using AES-256-GCM with a newly generated random 96-bit nonce.
    /// </summary>
    public void Encrypt(ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> nonce, Span<byte> tag)
    {
        RandomNumberGenerator.Fill(nonce);
        _aesGcm.Value.Encrypt(nonce, plaintext, ciphertext, tag);
    }

    /// <summary>
    /// Decrypts ciphertext and verifies the authentication tag using AES-256-GCM.
    /// </summary>
    public byte[] Decrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag)
    {
        var plaintext = new byte[ciphertext.Length];
        _aesGcm.Value.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    /// <summary>
    /// Decrypts ciphertext and verifies the authentication tag into a caller-provided plaintext span.
    /// </summary>
    public void Decrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext)
    {
        _aesGcm.Value.Decrypt(nonce, ciphertext, tag, plaintext);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && _aesGcm.IsValueCreated)
            _aesGcm.Value.Dispose();
    }
}

/// <summary>
/// Represents the result of an AES-GCM encryption operation, containing ciphertext, nonce, and authentication tag.
/// </summary>
public readonly record struct AesGcmEncryptedData(byte[] Ciphertext, byte[] Nonce, byte[] Tag);
