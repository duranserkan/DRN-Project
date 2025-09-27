using System.IO.Hashing;
using System.Security.Cryptography;
using Blake3;
using DRN.Framework.Utils.Encodings;

namespace DRN.Framework.Utils.Extensions;

public static class HashExtensions
{
    public static string HashOfFileWithKey(this string filePath, BinaryData key,
        HashAlgorithmSecure algorithm = HashAlgorithmSecure.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded) =>
        File.Exists(filePath)
            ? new BinaryData(File.ReadAllBytes(filePath)).HashWithKey(key, algorithm, encoding)
            : string.Empty;

    public static string HashOfFile(this string filePath,
        HashAlgorithm algorithm = HashAlgorithm.Blake3,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded) =>
        File.Exists(filePath)
            ? new BinaryData(File.ReadAllBytes(filePath)).Hash(algorithm, encoding)
            : string.Empty;

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

    private static Hash GetBlake3HashWithKey(BinaryData bytes, BinaryData key)
    {
        using var hasher = Hasher.NewKeyed(key);
        hasher.Update(bytes);

        return hasher.Finalize();
    }

    public static long GenerateSeedFromInputHash(this string input, byte hashStartIndex = 19)
    {
        hashStartIndex = hashStartIndex <= 32 ? hashStartIndex : (byte)19;

        return BitConverter.ToInt64(input.HashToBinary().ToMemory().Span[hashStartIndex..]);
    }
}

public enum HashAlgorithm
{
    Sha256 = 1,
    Sha512,
    Blake3,
    // ReSharper disable once InconsistentNaming
    XxHash3_64
}

public enum HashAlgorithmSecure
{
    Blake3With32CharKey = 1
}