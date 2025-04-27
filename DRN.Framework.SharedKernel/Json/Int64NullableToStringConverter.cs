using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Json;
//todo add tests
/// <summary>
/// Emits 64-bit integers as JSON numbers if within JS-safe range;
/// otherwise emits them as JSON strings to avoid precision loss.
/// </summary>
public class Int64NullableToStringConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt64();
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Unexpected token {reader.TokenType} when parsing Int64?. Expected Number, String, or Null.");

        var stringValue = reader.GetString();
        if (long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            return result;

        throw new JsonException($"Cannot convert \"{stringValue}\" to Int64.");
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        long actual = value.Value;

        // If outside JS safe range, serialize as string to avoid precision loss
        //https://spec.openapis.org/registry/format/int64
        //https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Number
        if (actual > IntegerSafeIntervalForJs.Max || actual < IntegerSafeIntervalForJs.Min)
            writer.WriteStringValue(actual.ToString(CultureInfo.InvariantCulture));
        else
            writer.WriteNumberValue(actual);
    }
}