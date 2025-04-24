using System.Buffers.Text;
using System.Text.Json;
using System.Text;

namespace DRN.Framework.Utils.Encodings;

public static class Base64Utils
{
    public static string UrlSafeBase64Encode(string input) => UrlSafeBase64Encode(Encoding.UTF8.GetBytes(input));
    public static string UrlSafeBase64Encode(byte[] input) => Base64Url.EncodeToString(input);
    public static string UrlSafeBase64Encode(Span<byte> input) => Base64Url.EncodeToString(input);
    public static string UrlSafeBase64Encode<TModel>(TModel model) => UrlSafeBase64Encode(JsonSerializer.SerializeToUtf8Bytes(model));

    public static byte[] UrlSafeBase64DecodeToBytes(string input) => Base64Url.DecodeFromChars(input.AsSpan());
    public static string UrlSafeBase64Decode(string input)
    {
        var array = Base64Url.DecodeFromChars(input.AsSpan());
        var decodedString = Encoding.UTF8.GetString(array);

        return decodedString;
    }

    public static TModel? UrlSafeBase64Decode<TModel>(string input)
    {
        var decodedJson = UrlSafeBase64Decode(input);
        var model = JsonSerializer.Deserialize<TModel>(decodedJson);

        return model;
    }
}