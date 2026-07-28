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
/// Concurrent encryption and decryption were verified against the .NET 10.0.10 runtime implementations.
/// Dispose only after all callers finish.
/// This primitive is deterministic and provides no authentication; do not compose it into multi-block ECB encryption.
/// </remarks>
public sealed class Aes256 : IDisposable
{
    private InlineArray15<Vector128<byte>> _decryptionRoundKeys;
    private InlineArray15<Vector128<byte>> _encryptionRoundKeys;
    private readonly Aes _portableAes;
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
    /// <b>Thread-safety verification for .NET 10.0.10:</b>
    /// Although the general .NET documentation states that instance members of <see cref="Aes"/> are not
    /// guaranteed to be thread-safe, the .NET 10.0.10 span-based one-shot <c>EncryptEcb</c> and <c>DecryptEcb</c>
    /// implementations create per-call cipher state and do not mutate the configured key after construction.
    /// Therefore, this wrapper does not serialize portable operations with a framework-level lock.
    /// </para>
    /// <para>
    /// Platform-specific .NET 10.0.10 implementation details:
    /// <list type="bullet">
    /// <item><description><b>Windows (CNG):</b> Uses a per-call BCrypt-backed cipher over the native CNG key handle.</description></item>
    /// <item><description><b>macOS (Apple CommonCrypto):</b> Uses a per-call cryptor backed by the stateless <c>CCCrypt</c> function.</description></item>
    /// <item><description><b>Linux (OpenSSL):</b> Uses a per-call cipher with its own <c>SafeEvpCipherCtxHandle</c>.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Concurrent use is covered directly by <c>Aes256_Should_Support_Parallel_Use_Of_One_Instance</c> and
    /// transitively by <c>SourceKnownEntityIdUtils_Should_Generate_Ids_For_3_Seconds</c>, which generates and
    /// parses 786,429 secure identifiers across eight parallel workers.
    /// </para>
    /// <para>
    /// For one 128-bit block, ECB is mathematically identical to CBC with a zero IV
    /// (<c>C = AES(Key, P XOR 0) = AES(Key, P)</c>) and avoids IV allocation and XOR overhead.
    /// The multi-block pattern-repetition weakness does not arise within one call because the call contains
    /// exactly one block. Repeated equal blocks under the same key still produce equal ciphertext, so this
    /// primitive remains deterministic and unauthenticated.
    /// </para>
    /// <para>
    /// Do not dispose the instance while an encryption or decryption operation is active. Reverify these
    /// implementation assumptions when changing the target runtime.
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
    /// The thread-safety verification, platform details, and single-block rationale are documented on
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
        _portableAes.EncryptEcb(input, output, PaddingMode.None);
        return Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(output));
    }

    private Vector128<byte> DecryptWithFrameworkCore(Vector128<byte> block)
    {
        Span<byte> input = stackalloc byte[Vector128<byte>.Count];
        Span<byte> output = stackalloc byte[Vector128<byte>.Count];
        block.StoreUnsafe(ref MemoryMarshal.GetReference(input));
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
        if (_disposed != 0)
            throw new ObjectDisposedException(nameof(Aes256));
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
