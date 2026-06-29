using Blake3;

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
    public const int KeyLength = 32;

    public static BinaryData Derive32ByteKey(ReadOnlySpan<byte> keyMaterial, string context)
    {
        Span<byte> derived = stackalloc byte[KeyLength];
        using var hasher = Hasher.NewDeriveKey(context);
        hasher.Update(keyMaterial);
        hasher.Finalize(derived);

        return BinaryData.FromBytes(derived.ToArray());
    }

    public static BinaryData Derive32ByteKey(BinaryData keyMaterial, string context)
        => Derive32ByteKey(keyMaterial.ToMemory().Span, context);
}
