using System.IO.Hashing;
using System.Security.Cryptography;
using Blake3;
using DRN.Framework.Utils.Encodings;

namespace DRN.Framework.Utils.Extensions;

//todo write tests
public static class HashExtensions
{
    public static string GetHashOfFile(this string filePath, HashAlgorithm algorithm, ByteEncoding encoding) => File.Exists(filePath)
        ? new BinaryData(File.ReadAllBytes(filePath)).GetHash(algorithm, encoding)
        : string.Empty;

    public static string GetHash(this string value, HashAlgorithm algorithm, ByteEncoding encoding)
        => new BinaryData(value).GetHash(algorithm, encoding);

    public static string GetHash(this byte[] value, HashAlgorithm algorithm, ByteEncoding encoding)
        => new BinaryData(value).GetHash(algorithm, encoding);

    public static string GetHash(this ReadOnlyMemory<byte> value, HashAlgorithm algorithm, ByteEncoding encoding)
        => new BinaryData(value).GetHash(algorithm, encoding);

    public static string GetHash(this BinaryData bytes, HashAlgorithm algorithm, ByteEncoding encoding, BinaryData? key = null)
    {
        var hashBytes = algorithm switch
        {
            HashAlgorithm.Blake3 => Hasher.Hash(bytes).AsSpan(),
            HashAlgorithm.Blake3WithKey => GetBlake3HashWithKey(bytes, key ?? BinaryData.Empty).AsSpan(),
            HashAlgorithm.XxHash3 => XxHash3.Hash(bytes),
            HashAlgorithm.XxHash128 => XxHash128.Hash(bytes),
            HashAlgorithm.XxHash64 => XxHash64.Hash(bytes),
            HashAlgorithm.Sha256 => SHA256.HashData(bytes),
            HashAlgorithm.Sha512 => SHA512.HashData(bytes),
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
    Blake3WithKey,
    XxHash64,
    XxHash3,
    XxHash128,
}