using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using ArmAes = System.Runtime.Intrinsics.Arm.Aes;
using X86Aes = System.Runtime.Intrinsics.X86.Aes;

namespace DRN.Framework.Utils.Data.Encryption;

/// <summary>
/// AES-256 for one 128-bit ECB block with runtime-intrinsic, portable, and automatic fallback paths.
/// </summary>
/// <remarks>
/// Supports concurrent encryption and decryption. Portable provider operations are serialized per instance.
/// Dispose only after all callers finish.
/// This primitive is deterministic and provides no authentication; do not compose it into multi-block ECB encryption.
/// </remarks>
public sealed class Aes256 : IDisposable
{
    private InlineArray15<Vector128<byte>> _decryptionRoundKeys;
    private InlineArray15<Vector128<byte>> _encryptionRoundKeys;
    private readonly Aes _portableAes;
    private readonly Lock _portableAesLock = new();
    private int _disposed;

    public Aes256(ReadOnlySpan<byte> key)
    {
        if (key.Length != Aes256KeyPreparation.KeySizeInBytes)
            throw new ArgumentException(
                $"AES-256 requires a {Aes256KeyPreparation.KeySizeInBytes}-byte key; received {key.Length}.",
                nameof(key));

        _portableAes = Aes256KeyPreparation.CreatePortableAes(key);
        if (!IsSupported)
            return;

        try
        {
            Aes256KeyPreparation.PrepareIntrinsicSchedules(key, ref _encryptionRoundKeys, ref _decryptionRoundKeys);
        }
        catch
        {
            _portableAes.Dispose();
            Clear(ref _encryptionRoundKeys);
            Clear(ref _decryptionRoundKeys);
            throw;
        }
    }

    /// <summary>
    /// Gets whether the current runtime supports the x86 or ARM AES intrinsics used by the explicit intrinsic methods.
    /// </summary>
    public static bool IsSupported => X86Aes.IsSupported || ArmAes.IsSupported;

    /// <summary>
    /// Encrypts one block with runtime intrinsics when supported and the portable provider otherwise.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector128<byte> Encrypt(Vector128<byte> block)
    {
        if (!IsSupported)
            return EncryptWithFramework(block);

        ThrowIfDisposed();
        return EncryptRuntimeIntrinsicsCore(block);
    }

    /// <summary>
    /// Decrypts one block with runtime intrinsics when supported and the portable provider otherwise.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector128<byte> Decrypt(Vector128<byte> block)
    {
        if (!IsSupported)
            return DecryptWithFramework(block);

        ThrowIfDisposed();
        return DecryptRuntimeIntrinsicsCore(block);
    }

    /// <summary>
    /// Encrypts one block with x86 or ARM AES runtime intrinsics.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">The current runtime does not support AES intrinsics.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector128<byte> EncryptRuntimeIntrinsics(Vector128<byte> block)
    {
        ThrowIfDisposed();
        ThrowIfRuntimeIntrinsicsUnsupported();
        return EncryptRuntimeIntrinsicsCore(block);
    }

    /// <summary>
    /// Decrypts one block with x86 or ARM AES runtime intrinsics.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">The current runtime does not support AES intrinsics.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector128<byte> DecryptRuntimeIntrinsics(Vector128<byte> block)
    {
        ThrowIfDisposed();
        ThrowIfRuntimeIntrinsicsUnsupported();
        return DecryptRuntimeIntrinsicsCore(block);
    }

    /// <summary>
    /// Encrypts one block with the portable .NET AES provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Portable encryption and decryption share one lock because the underlying <see cref="Aes"/>
    /// instance does not support concurrent operations. Runtime-intrinsic operations do not acquire this lock.
    /// </para>
    /// <para>
    /// For one 128-bit block, ECB is mathematically identical to CBC with a zero IV
    /// (<c>C = AES(Key, P XOR 0) = AES(Key, P)</c>) and avoids IV allocation and XOR overhead.
    /// The multi-block pattern-repetition weakness does not arise within one call because the call contains
    /// exactly one block. Repeated equal blocks under the same key still produce equal ciphertext, so this
    /// primitive remains deterministic and unauthenticated.
    /// </para>
    /// <para>
    /// Do not dispose the instance while an encryption or decryption operation is active.
    /// </para>
    /// </remarks>
    public Vector128<byte> EncryptWithFramework(Vector128<byte> block)
    {
        ThrowIfDisposed();
        return EncryptWithFrameworkCore(block);
    }

    /// <summary>
    /// Decrypts one block with the portable .NET AES provider.
    /// </summary>
    /// <remarks>
    /// The concurrency contract and single-block rationale are documented on
    /// <see cref="EncryptWithFramework"/>.
    /// </remarks>
    public Vector128<byte> DecryptWithFramework(Vector128<byte> block)
    {
        ThrowIfDisposed();
        return DecryptWithFrameworkCore(block);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            _portableAes.Dispose();
        }
        finally
        {
            Clear(ref _encryptionRoundKeys);
            Clear(ref _decryptionRoundKeys);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector128<byte> EncryptRuntimeIntrinsicsCore(Vector128<byte> block)
        => X86Aes.IsSupported ? EncryptX86(block) : EncryptArm(block);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector128<byte> DecryptRuntimeIntrinsicsCore(Vector128<byte> block)
        => X86Aes.IsSupported ? DecryptX86(block) : DecryptArm(block);

    private Vector128<byte> EncryptWithFrameworkCore(Vector128<byte> block)
    {
        Span<byte> input = stackalloc byte[Vector128<byte>.Count];
        Span<byte> output = stackalloc byte[Vector128<byte>.Count];
        block.StoreUnsafe(ref MemoryMarshal.GetReference(input));
        lock (_portableAesLock)
            _portableAes.EncryptEcb(input, output, PaddingMode.None);
        return Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(output));
    }

    private Vector128<byte> DecryptWithFrameworkCore(Vector128<byte> block)
    {
        Span<byte> input = stackalloc byte[Vector128<byte>.Count];
        Span<byte> output = stackalloc byte[Vector128<byte>.Count];
        block.StoreUnsafe(ref MemoryMarshal.GetReference(input));
        lock (_portableAesLock)
            _portableAes.DecryptEcb(input, output, PaddingMode.None);
        return Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(output));
    }

    private Vector128<byte> EncryptX86(Vector128<byte> block)
    {
        var value = Vector128.Xor(block, _encryptionRoundKeys[0]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[1]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[2]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[3]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[4]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[5]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[6]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[7]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[8]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[9]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[10]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[11]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[12]);
        value = X86Aes.Encrypt(value, _encryptionRoundKeys[13]);
        return X86Aes.EncryptLast(value, _encryptionRoundKeys[14]);
    }

    private Vector128<byte> DecryptX86(Vector128<byte> block)
    {
        var value = Vector128.Xor(block, _decryptionRoundKeys[0]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[1]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[2]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[3]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[4]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[5]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[6]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[7]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[8]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[9]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[10]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[11]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[12]);
        value = X86Aes.Decrypt(value, _decryptionRoundKeys[13]);
        return X86Aes.DecryptLast(value, _decryptionRoundKeys[14]);
    }

    private Vector128<byte> EncryptArm(Vector128<byte> block)
    {
        var value = ArmAes.MixColumns(ArmAes.Encrypt(block, _encryptionRoundKeys[0]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[1]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[2]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[3]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[4]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[5]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[6]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[7]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[8]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[9]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[10]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[11]));
        value = ArmAes.MixColumns(ArmAes.Encrypt(value, _encryptionRoundKeys[12]));
        value = ArmAes.Encrypt(value, _encryptionRoundKeys[13]);
        return Vector128.Xor(value, _encryptionRoundKeys[14]);
    }

    private Vector128<byte> DecryptArm(Vector128<byte> block)
    {
        var value = ArmAes.InverseMixColumns(ArmAes.Decrypt(block, _decryptionRoundKeys[0]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[1]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[2]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[3]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[4]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[5]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[6]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[7]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[8]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[9]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[10]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[11]));
        value = ArmAes.InverseMixColumns(ArmAes.Decrypt(value, _decryptionRoundKeys[12]));
        value = ArmAes.Decrypt(value, _decryptionRoundKeys[13]);
        return Vector128.Xor(value, _decryptionRoundKeys[14]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfRuntimeIntrinsicsUnsupported()
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException("AES runtime intrinsics are not supported.");
    }

    private static void Clear(ref InlineArray15<Vector128<byte>> keys)
    {
        var vectors = MemoryMarshal.CreateSpan(ref keys[0], Aes256KeyPreparation.RoundKeyCount);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(vectors));
    }
}
