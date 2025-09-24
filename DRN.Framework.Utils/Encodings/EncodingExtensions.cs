using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace DRN.Framework.Utils.Encodings;

public static class EncodingExtensions
{
    public static readonly Encoding Utf8 = Encoding.UTF8;

    public static string Encode(this byte[] bytes, ByteEncoding encoding) => Encode(new BinaryData(bytes), encoding);
    public static string Encode(this BinaryData bytes, ByteEncoding encoding) => Encode(bytes.ToMemory().Span, encoding);

    public static string Encode(this Span<byte> bytes, ByteEncoding encoding)
    {
        ReadOnlySpan<byte> readOnlySpan = bytes;

        return Encode(readOnlySpan, encoding);
    }

    public static string Encode<TModel>(this TModel model, ByteEncoding encoding) => JsonSerializer.SerializeToUtf8Bytes(model).UrlSafeBase64Encode();

    public static string Encode(this ReadOnlySpan<byte> bytes, ByteEncoding encoding) => encoding switch
    {
        ByteEncoding.Base64 => Convert.ToBase64String(bytes),
        ByteEncoding.Base64UrlEncoded => bytes.UrlSafeBase64Encode(),
        ByteEncoding.Hex => Convert.ToHexString(bytes),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
    };

    public static TModel? Decode<TModel>(this string input, ByteEncoding encoding) => JsonSerializer.Deserialize<TModel>(input.Decode(encoding));

    public static BinaryData Decode(this string input, ByteEncoding encoding) => encoding switch
    {
        ByteEncoding.Base64 => new BinaryData(Convert.FromBase64String(input)),
        ByteEncoding.Base64UrlEncoded => new BinaryData(input.UrlSafeBase64DecodeToBinary().ToArray()),
        ByteEncoding.Hex => input.HexDecodeToBinary(),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
    };

    public static string DecodeAsString(this string input, ByteEncoding encoding) => input.Decode(encoding).ToString();
    
    private static string UrlSafeBase64Encode(this byte[] input) => Base64Url.EncodeToString(input);
    private static string UrlSafeBase64Encode(this ReadOnlySpan<byte> input) => Base64Url.EncodeToString(input);

    private static BinaryData UrlSafeBase64DecodeToBinary(this string input) => new(Base64Url.DecodeFromChars(input.AsSpan()));
    private static BinaryData HexDecodeToBinary(this string hex) => new(Convert.FromHexString(hex));
}

public enum ByteEncoding
{
    Hex = 1,
    Base64,
    Base64UrlEncoded
}