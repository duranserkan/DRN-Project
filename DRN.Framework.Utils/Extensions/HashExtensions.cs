using System.Security.Cryptography;

namespace DRN.Framework.Utils.Extensions;

public static class HashExtensions
{
    private const string HexChars = "0123456789abcdef";

    public static string ComputeSha256HashBase64UrlEncoded(this string filePath)
    {
        if (!File.Exists(filePath))
            return string.Empty;

        using var stream = File.OpenRead(filePath);
        return stream.ComputeSha256HashBase64UrlEncoded();
    }

    public static string ComputeSha256HashBase64UrlEncoded(this Stream stream, bool dispose = false)
    {
        var hash = SHA256.HashData(stream);

        if (dispose)
            stream.Dispose();

        return hash.UrlSafeBase64Encode();
    }

    
    public static string GetSha512HashHexEncoded(this string value)
    {
        var hashBytes = SHA512.HashData(value.ToByteArray());

        return string.Create(hashBytes.Length * 2, hashBytes, (chars, bytes) =>
        {
            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                chars[i * 2] = HexChars[b >> 4]; // the high-order 4 bits(nibbles) (first hex digit)
                chars[i * 2 + 1] = HexChars[b & 0x0F]; // the low-order 4 bits(nibbles) (second hex digit):
            }
        });
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