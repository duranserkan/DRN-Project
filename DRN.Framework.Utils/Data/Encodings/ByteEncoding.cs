using System.Text.Json.Serialization;

namespace DRN.Framework.Utils.Data.Encodings;

[JsonConverter(typeof(JsonStringEnumConverter<ByteEncoding>))]
public enum ByteEncoding
{
    Hex = 1,
    Base64,
    Base64UrlEncoded,
    Utf8
}
