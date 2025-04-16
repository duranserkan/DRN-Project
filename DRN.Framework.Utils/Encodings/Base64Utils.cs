using System.Buffers.Text;
using System.Text.Json;

namespace DRN.Framework.Utils.Encodings;

public static class Base64Utils
{
    public static string UrlSafeBase64Encode(string input)
    {
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        var encodedString = Base64Url.EncodeToString(inputBytes);

        return encodedString;
    }

    public static string UrlSafeBase64Encode<TModel>(TModel model)
    {
        var json = JsonSerializer.Serialize(model);
        var encodedJson= UrlSafeBase64Encode(json);
        
        return encodedJson;
    }

    public static string UrlSafeBase64Decode(string input)
    {
        var array = Base64Url.DecodeFromChars(input.ToCharArray());
        var decodedString = System.Text.Encoding.UTF8.GetString(array);

        return decodedString;
    }
    
    public static TModel? UrlSafeBase64Decode<TModel>(string input)
    {
        var decodedJson = UrlSafeBase64Decode(input);
        var model = JsonSerializer.Deserialize<TModel>(decodedJson);

        return model;
    }
}