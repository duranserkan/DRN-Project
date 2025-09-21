namespace DRN.Framework.Utils.Extensions;

public static class EncodingExtensions
{
    private const string HexChars = "0123456789abcdef";

    public static string Encode(this byte[] bytes, ByteEncoding encoding) => Encode(new BinaryData(bytes), encoding);
    public static string Encode(this BinaryData bytes, ByteEncoding encoding) => Encode(bytes.ToMemory().Span, encoding);

    public static string Encode(this Span<byte> bytes, ByteEncoding encoding)
    {
        ReadOnlySpan<byte> readOnlySpan = bytes;

        return Encode(readOnlySpan, encoding);
    }

    public static string Encode(this ReadOnlySpan<byte> bytes, ByteEncoding encoding) => encoding switch
    {
        ByteEncoding.Base64 => Convert.ToBase64String(bytes),
        ByteEncoding.Base64UrlEncoded => bytes.UrlSafeBase64Encode(),
        ByteEncoding.Hex => bytes.HexEncode(),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
    };

    public static string UrlSafeBase64Encode(this BinaryData data)
    {
        var base64 = Convert.ToBase64String(data);
        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static string UrlSafeBase64Encode(this ReadOnlySpan<byte> data)
    {
        var base64 = Convert.ToBase64String(data);
        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static string HexEncode(this ReadOnlySpan<byte> hashBytes) => string.Create(hashBytes.Length * 2, hashBytes, (chars, bytes) =>
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i * 2] = HexChars[b >> 4]; // the high-order 4 bits(nibbles) (first hex digit)
            chars[i * 2 + 1] = HexChars[b & 0x0F]; // the low-order 4 bits(nibbles) (second hex digit):
        }
    });

    public static BinaryData UrlSafeBase64Decode(this string base64UrlSafe)
    {
        var base64 = base64UrlSafe
            .Replace('-', '+')
            .Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return base64.Base64Decode();
    }

    public static BinaryData Base64Decode(this string base64) => new(Convert.FromBase64String(base64));
}

public enum ByteEncoding
{
    Hex = 1,
    Base64,
    Base64UrlEncoded
}