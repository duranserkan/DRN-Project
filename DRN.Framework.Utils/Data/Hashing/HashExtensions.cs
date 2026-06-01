using System.Buffers;
using System.IO.Hashing;
using System.Security.Cryptography;
using Blake3;
using DRN.Framework.Utils.Data.Encodings;

namespace DRN.Framework.Utils.Data.Hashing;

public static class HashExtensions
{
    /// <summary>
    /// Default buffer size for streaming data into BLAKE3; 16 KiB is large enough to leverage BLAKE3's currently supported SIMD paths.
    /// </summary>
    private const int Blake3StreamBufferSize = 16 * 1024;

    public static string HashOfFileWithKey(this string filePath, BinaryData key,
        HashAlgorithmSecure algorithm = HashAlgorithmSecure.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
    {
        if (!File.Exists(filePath))
            return string.Empty;

        using var stream = File.OpenRead(filePath);
        return stream.HashWithKey(key, algorithm, encoding);
    }

    public static string HashOfFile(this string filePath,
        HashAlgorithm algorithm = HashAlgorithm.Blake3,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
    {
        if (!File.Exists(filePath))
            return string.Empty;

        using var stream = File.OpenRead(filePath);
        return stream.Hash(algorithm, encoding);
    }

    public static string Hash(this string value,
        HashAlgorithm algorithm = HashAlgorithm.Blake3,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).Hash(algorithm, encoding);

    public static string Hash(this byte[] value,
        HashAlgorithm algorithm = HashAlgorithm.Blake3,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).Hash(algorithm, encoding);

    public static string Hash(this ReadOnlyMemory<byte> value,
        HashAlgorithm algorithm = HashAlgorithm.Blake3,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).Hash(algorithm, encoding);

    public static string Hash(this Stream stream,
        HashAlgorithm algorithm = HashAlgorithm.Blake3,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return algorithm switch
        {
            HashAlgorithm.Blake3 => GetBlake3Hash(stream).AsSpan().Encode(encoding),
            HashAlgorithm.XxHash3_64 => GetXxHash3Hash(stream, encoding),
            HashAlgorithm.Sha256 => GetSha256Hash(stream, encoding),
            HashAlgorithm.Sha512 => GetSha512Hash(stream, encoding),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };
    }

    public static string Hash(this BinaryData bytes,
        HashAlgorithm algorithm = HashAlgorithm.Blake3,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
    {
        var hashBytes = algorithm switch
        {
            HashAlgorithm.Blake3 => Hasher.Hash(bytes).AsSpan(),
            HashAlgorithm.XxHash3_64 => XxHash3.Hash(bytes),
            HashAlgorithm.Sha256 => SHA256.HashData(bytes),
            HashAlgorithm.Sha512 => SHA512.HashData(bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };

        var hash = hashBytes.Encode(encoding);
        return hash;
    }

    public static BinaryData HashToBinary(this string value, HashAlgorithm algorithm = HashAlgorithm.Blake3)
        => new BinaryData(value).HashToBinary(algorithm);

    public static BinaryData HashToBinary(this byte[] value, HashAlgorithm algorithm = HashAlgorithm.Blake3)
        => new BinaryData(value).HashToBinary(algorithm);

    public static BinaryData HashToBinary(this ReadOnlyMemory<byte> value, HashAlgorithm algorithm = HashAlgorithm.Blake3)
        => new BinaryData(value).HashToBinary(algorithm);

    public static BinaryData HashToBinary(this Stream stream, HashAlgorithm algorithm = HashAlgorithm.Blake3)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var hashBytes = algorithm switch
        {
            HashAlgorithm.Blake3 => GetBlake3Hash(stream).AsSpan().ToArray(),
            HashAlgorithm.XxHash3_64 => GetXxHash3Hash(stream),
            HashAlgorithm.Sha256 => SHA256.HashData(stream),
            HashAlgorithm.Sha512 => SHA512.HashData(stream),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };

        var hash = BinaryData.FromBytes(hashBytes);
        return hash;
    }

    public static BinaryData HashToBinary(this BinaryData bytes, HashAlgorithm algorithm = HashAlgorithm.Blake3)
    {
        var hashBytes = algorithm switch
        {
            HashAlgorithm.Blake3 => Hasher.Hash(bytes).AsSpan().ToArray(),
            HashAlgorithm.XxHash3_64 => XxHash3.Hash(bytes),
            HashAlgorithm.Sha256 => SHA256.HashData(bytes),
            HashAlgorithm.Sha512 => SHA512.HashData(bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };

        var hash = BinaryData.FromBytes(hashBytes);
        return hash;
    }

    public static ulong Hash64Bit(this byte[] value, long seed = 0) => XxHash3.HashToUInt64(value, seed);
    public static ulong Hash64Bit(this Span<byte> value, long seed = 0) => XxHash3.HashToUInt64(value, seed);
    public static ulong Hash64Bit(this ReadOnlySpan<byte> value, long seed = 0) => XxHash3.HashToUInt64(value, seed);
    public static ulong Hash64Bit(this ReadOnlyMemory<byte> value, long seed = 0) => new BinaryData(value).Hash64Bit(seed);
    public static ulong Hash64Bit(this Stream value, long seed = 0) => GetXxHash3Hash64Bit(value, seed);
    public static ulong Hash64Bit(this string value, long seed = 0) => BinaryData.FromString(value).Hash64Bit(seed);
    public static ulong Hash64Bit(this BinaryData value, long seed = 0) => XxHash3.HashToUInt64(value, seed);

    public static string HashWithKey(this string value, BinaryData key,
        HashAlgorithmSecure algorithm = HashAlgorithmSecure.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).HashWithKey(key, algorithm, encoding);

    public static string HashWithKey(this byte[] value, BinaryData key,
        HashAlgorithmSecure algorithm = HashAlgorithmSecure.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).HashWithKey(key, algorithm, encoding);

    public static string HashWithKey(this ReadOnlyMemory<byte> value, BinaryData key,
        HashAlgorithmSecure algorithm = HashAlgorithmSecure.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).HashWithKey(key, algorithm, encoding);

    public static string HashWithKey(this Stream stream, BinaryData key,
        HashAlgorithmSecure algorithm = HashAlgorithmSecure.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var hashBytes = algorithm switch
        {
            HashAlgorithmSecure.Blake3With32CharKey => GetBlake3HashWithKey(stream, key).AsSpan(),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };

        var hash = hashBytes.Encode(encoding);

        return hash;
    }

    public static string HashWithKey(this BinaryData bytes, BinaryData key,
        HashAlgorithmSecure algorithm = HashAlgorithmSecure.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
    {
        var hashBytes = algorithm switch
        {
            HashAlgorithmSecure.Blake3With32CharKey => GetBlake3HashWithKey(bytes, key).AsSpan(),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };

        var hash = hashBytes.Encode(encoding);

        return hash;
    }

    private static Hash GetBlake3Hash(Stream stream)
    {
        // Hasher is a mutable struct passed by ref to UpdateBlake3Hasher.
        // A 'using' variable is read-only and cannot be passed as a ref parameter.
        // Hence, we must use a manual try/finally block to ensure proper disposal.
        var hasher = Hasher.New();

        try
        {
            UpdateBlake3Hasher(stream, ref hasher);
            return hasher.Finalize();
        }
        finally
        {
            hasher.Dispose();
        }
    }

    private static Hash GetBlake3HashWithKey(BinaryData bytes, BinaryData key)
    {
        using var hasher = Hasher.NewKeyed(key);
        hasher.Update(bytes);

        return hasher.Finalize();
    }

    private static Hash GetBlake3HashWithKey(Stream stream, BinaryData key)
    {
        // Hasher is a mutable struct passed by ref to UpdateBlake3Hasher.
        // A 'using' variable is read-only and cannot be passed as a ref parameter.
        // Hence, we must use a manual try/finally block to ensure proper disposal.
        var hasher = Hasher.NewKeyed(key);

        try
        {
            UpdateBlake3Hasher(stream, ref hasher);
            return hasher.Finalize();
        }
        finally
        {
            hasher.Dispose();
        }
    }

    private static void UpdateBlake3Hasher(Stream stream, ref Hasher hasher)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Blake3StreamBufferSize);

        try
        {
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                hasher.Update(buffer.AsSpan(0, bytesRead));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static byte[] GetXxHash3Hash(Stream stream)
    {
        var hasher = new XxHash3(0);
        hasher.Append(stream);

        return hasher.GetCurrentHash();
    }

    private static string GetXxHash3Hash(Stream stream, ByteEncoding encoding)
    {
        Span<byte> hashBytes = stackalloc byte[sizeof(ulong)];
        var hasher = new XxHash3(0);
        hasher.Append(stream);
        hasher.GetCurrentHash(hashBytes);

        return hashBytes.Encode(encoding);
    }

    private static ulong GetXxHash3Hash64Bit(Stream stream, long seed)
    {
        var hasher = new XxHash3(seed);
        hasher.Append(stream);

        return hasher.GetCurrentHashAsUInt64();
    }

    private static string GetSha256Hash(Stream stream, ByteEncoding encoding)
    {
        Span<byte> hashBytes = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(stream, hashBytes);

        return hashBytes.Encode(encoding);
    }

    private static string GetSha512Hash(Stream stream, ByteEncoding encoding)
    {
        Span<byte> hashBytes = stackalloc byte[SHA512.HashSizeInBytes];
        SHA512.HashData(stream, hashBytes);

        return hashBytes.Encode(encoding);
    }

    public static long GenerateSeedFromInputHash(this string input, byte hashStartIndex = 19)
    {
        hashStartIndex = hashStartIndex <= 32 ? hashStartIndex : (byte)19;

        return BitConverter.ToInt64(input.HashToBinary().ToMemory().Span[hashStartIndex..]);
    }
}
