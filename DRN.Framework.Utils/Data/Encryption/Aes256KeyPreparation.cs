using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using ArmAes = System.Runtime.Intrinsics.Arm.Aes;
using X86Aes = System.Runtime.Intrinsics.X86.Aes;

namespace DRN.Framework.Utils.Data.Encryption;

internal static class Aes256KeyPreparation
{
    internal const int KeySizeInBytes = 32;
    private const int RoundCount = 14;
    internal const int RoundKeyCount = RoundCount + 1;
    private const int ExpandedKeyWordCount = RoundKeyCount * 4;

    internal static Aes CreatePortableAes(ReadOnlySpan<byte> key)
    {
        var keyBytes = key.ToArray();
        try
        {
            var aes = Aes.Create();
            try
            {
                aes.KeySize = KeySizeInBytes * 8;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key = keyBytes;
                return aes;
            }
            catch
            {
                aes.Dispose();
                throw;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    internal static void PrepareIntrinsicSchedules(
        ReadOnlySpan<byte> key,
        ref InlineArray15<Vector128<byte>> encryptionRoundKeys,
        ref InlineArray15<Vector128<byte>> decryptionRoundKeys)
    {
        ExpandKey(key, ref encryptionRoundKeys);
        CreateDecryptionSchedule(ref encryptionRoundKeys, ref decryptionRoundKeys);
    }

    private static void ExpandKey(ReadOnlySpan<byte> key, ref InlineArray15<Vector128<byte>> encryptionRoundKeys)
    {
        Span<uint> words = stackalloc uint[ExpandedKeyWordCount];
        try
        {
            for (var index = 0; index < KeySizeInBytes / sizeof(uint); index++)
                words[index] = BinaryPrimitives.ReadUInt32BigEndian(key[(index * sizeof(uint))..]);

            uint roundConstant = 1;
            for (var index = KeySizeInBytes / sizeof(uint); index < ExpandedKeyWordCount; index++)
            {
                var previous = words[index - 1];
                if (index % 8 == 0)
                {
                    previous = SubWord(BitOperations.RotateLeft(previous, 8)) ^ (roundConstant << 24);
                    roundConstant <<= 1;
                }
                else if (index % 8 == 4)
                    previous = SubWord(previous);

                words[index] = words[index - 8] ^ previous;
            }

            for (var round = 0; round < RoundKeyCount; round++)
                encryptionRoundKeys[round] = CreateRoundKey(words, round * 4);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(words));
        }
    }

    private static void CreateDecryptionSchedule(
        ref InlineArray15<Vector128<byte>> encryptionRoundKeys,
        ref InlineArray15<Vector128<byte>> decryptionRoundKeys)
    {
        // Intermediate decryption keys are reversed and inverse-mixed once during construction.
        decryptionRoundKeys[0] = encryptionRoundKeys[RoundCount];
        for (var round = 1; round < RoundCount; round++)
        {
            var key = encryptionRoundKeys[RoundCount - round];
            decryptionRoundKeys[round] = X86Aes.IsSupported
                ? X86Aes.InverseMixColumns(key)
                : ArmAes.InverseMixColumns(key);
        }

        decryptionRoundKeys[RoundCount] = encryptionRoundKeys[0];
    }

    private static Vector128<byte> CreateRoundKey(ReadOnlySpan<uint> words, int offset)
        => Vector128.Create(
            BinaryPrimitives.ReverseEndianness(words[offset]),
            BinaryPrimitives.ReverseEndianness(words[offset + 1]),
            BinaryPrimitives.ReverseEndianness(words[offset + 2]),
            BinaryPrimitives.ReverseEndianness(words[offset + 3])).AsByte();

    private static uint SubWord(uint word)
    {
        // Repeated lanes make ShiftRows a no-op, leaving a constant-time hardware SubBytes transform.
        var value = Vector128.Create(word).AsByte();
        var result = X86Aes.IsSupported
            ? X86Aes.EncryptLast(value, Vector128<byte>.Zero)
            : ArmAes.Encrypt(value, Vector128<byte>.Zero);
        return result.AsUInt32().GetElement(0);
    }
}
