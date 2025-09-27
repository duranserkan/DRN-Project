using System.Buffers.Text;
using System.Text.Json;

namespace DRN.Framework.Utils.Encodings;

public static class EncodingExtensions
{
    public static string Encode(this byte[] bytes, ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => Encode(new BinaryData(bytes), encoding);

    public static string Encode(this BinaryData bytes, ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => Encode(bytes.ToMemory().Span, encoding);

    public static string Encode(this Span<byte> bytes, ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
    {
        ReadOnlySpan<byte> readOnlySpan = bytes;
        return Encode(readOnlySpan, encoding);
    }

    public static string Encode<TModel>(this TModel model, ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => JsonSerializer.SerializeToUtf8Bytes(model).Encode(encoding);

    public static string Encode(this ReadOnlySpan<byte> bytes, ByteEncoding encoding = ByteEncoding.Base64UrlEncoded) => encoding switch
    {
        ByteEncoding.Base64 => Convert.ToBase64String(bytes),
        ByteEncoding.Base64UrlEncoded => Base64Url.EncodeToString(bytes),
        ByteEncoding.Hex => Convert.ToHexString(bytes),
        ByteEncoding.Utf8 => BinaryData.FromBytes(bytes.ToArray()).ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
    };

    public static TModel? Decode<TModel>(this string input, ByteEncoding encoding = ByteEncoding.Base64UrlEncoded)
        => JsonSerializer.Deserialize<TModel>(input.Decode(encoding));

    public static BinaryData Decode(this string input, ByteEncoding encoding = ByteEncoding.Base64UrlEncoded) => encoding switch
    {
        ByteEncoding.Base64 => new BinaryData(Convert.FromBase64String(input)),
        ByteEncoding.Base64UrlEncoded => new(Base64Url.DecodeFromChars(input)),
        ByteEncoding.Hex => new(Convert.FromHexString(input)),
        ByteEncoding.Utf8 => BinaryData.FromString(input),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
    };

    public static string DecodeAsString(this string input, ByteEncoding encoding = ByteEncoding.Base64UrlEncoded) => input.Decode(encoding).ToString();
}

public enum ByteEncoding
{
    Hex = 1,
    Base64,
    Base64UrlEncoded,
    Utf8
}