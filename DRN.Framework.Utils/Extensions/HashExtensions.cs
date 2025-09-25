using System.IO.Hashing;
using System.Security.Cryptography;
using Blake3;
using DRN.Framework.Utils.Encodings;

namespace DRN.Framework.Utils.Extensions;

//todo add 64 bit hash extension with xxhash3
public static class HashExtensions
{
    public static string HashOfFile(this string filePath,
        HashAlgorithm algorithm = HashAlgorithm.XxHash3_64,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded) =>
        File.Exists(filePath)
            ? new BinaryData(File.ReadAllBytes(filePath)).Hash(algorithm, encoding)
            : string.Empty;

    public static string Hash(this string value,
        HashAlgorithm algorithm = HashAlgorithm.XxHash3_64,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).Hash(algorithm, encoding);

    public static string Hash(this byte[] value,
        HashAlgorithm algorithm = HashAlgorithm.XxHash3_64,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).Hash(algorithm, encoding);

    public static string Hash(this ReadOnlyMemory<byte> value,
        HashAlgorithm algorithm = HashAlgorithm.XxHash3_64,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).Hash(algorithm, encoding);

    public static string Hash(this BinaryData bytes,
        HashAlgorithm algorithm = HashAlgorithm.XxHash3_64,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
    {
        var hashBytes = algorithm switch
        {
            HashAlgorithm.Blake3 => Hasher.Hash(bytes).AsSpan(),
            HashAlgorithm.XxHash3_64 => XxHash3.Hash(bytes),
            HashAlgorithm.XxHash128 => XxHash128.Hash(bytes),
            HashAlgorithm.XxHash64 => XxHash64.Hash(bytes),
            HashAlgorithm.Sha256 => SHA256.HashData(bytes),
            HashAlgorithm.Sha512 => SHA512.HashData(bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };

        var hash = hashBytes.Encode(encoding);

        return hash;
    }

    public static string HashWithKey(this string value, BinaryData key,
        SecureHashAlgorithm algorithm = SecureHashAlgorithm.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).HashWithKey(key, algorithm, encoding);

    public static string HashWithKey(this byte[] value, BinaryData key,
        SecureHashAlgorithm algorithm = SecureHashAlgorithm.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).HashWithKey(key, algorithm, encoding);

    public static string HashWithKey(this ReadOnlyMemory<byte> value, BinaryData key,
        SecureHashAlgorithm algorithm = SecureHashAlgorithm.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => new BinaryData(value).HashWithKey(key, algorithm, encoding);

    public static string HashWithKey(this BinaryData bytes, BinaryData key,
        SecureHashAlgorithm algorithm = SecureHashAlgorithm.Blake3With32CharKey,
        ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
    {
        var hashBytes = algorithm switch
        {
            SecureHashAlgorithm.Blake3With32CharKey => GetBlake3HashWithKey(bytes, key).AsSpan(),
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

    public static long GenerateLongSeedFromHash(this string input)
    {
        var hashBytes = SHA512.HashData(input.ToByteArray());
        return BitConverter.ToInt64(hashBytes, 19);
    }

    public static int GenerateIntSeedFromHash(this string input)
    {
        var hashBytes = SHA256.HashData(input.ToByteArray());
        return BitConverter.ToInt32(hashBytes, 19);
    }
}

public enum HashAlgorithm
{
    Sha256 = 1,
    Sha512,
    Blake3,
    XxHash64,
    // ReSharper disable once InconsistentNaming
    XxHash3_64,
    XxHash128,
}

public enum SecureHashAlgorithm
{
    Blake3With32CharKey = 1
}