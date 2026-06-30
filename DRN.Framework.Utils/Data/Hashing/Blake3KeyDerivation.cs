using System.Security.Cryptography;
using Blake3;
using DRN.Framework.Utils.Data.Encryption;

namespace DRN.Framework.Utils.Data.Hashing;

/// <summary>
/// Provides helper methods for deriving subkeys using BLAKE3 Key Derivation Mode.
/// </summary>
/// <remarks>
/// For cryptographic specification and details on BLAKE3 subkey derivation, see:
/// <see href="https://docs.rs/blake3/latest/blake3/fn.derive_key.html"/>
/// </remarks>
internal static class Blake3KeyDerivation
{
    internal const int KeyLength = SecretKey32.KeyLength;

    internal static SecretKey32 Derive32ByteKey(ReadOnlySpan<byte> keyMaterial, string context)
    {
        Span<byte> derived = stackalloc byte[KeyLength];
        try
        {
            using var hasher = Hasher.NewDeriveKey(context);
            hasher.Update(keyMaterial);
            hasher.Finalize(derived);

            return new SecretKey32(derived);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derived);
        }
    }

    internal static SecretKey32 Derive32ByteKey(SecretKey32 keyMaterial, string context)
        => Derive32ByteKey(keyMaterial.Span, context);
}
