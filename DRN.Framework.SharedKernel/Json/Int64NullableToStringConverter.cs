using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Json;

/// <summary>
/// Emits 64-bit integers as JSON numbers if within JS-safe range;
/// otherwise emits them as JSON strings to avoid precision loss.
/// </summary>
public class Int64NullableToStringConverter : JsonConverter<long?>
{
    private static readonly Int64ToStringConverter Int64Converter = new();

    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : Int64Converter.Read(ref reader, typeof(long), options);

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            Int64Converter.Write(writer, value.Value, options);
        else
            writer.WriteNullValue();
    }
}
