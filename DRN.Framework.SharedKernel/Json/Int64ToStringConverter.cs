using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Json;
//todo add tests
/// <summary>
/// Emits 64-bit integers as JSON numbers if within JS-safe range;
/// otherwise emits them as JSON strings to avoid precision loss.
/// </summary>
public class Int64ToStringConverter : JsonConverter<long>
{
    /// <summary>
    /// The maximum integer value that JavaScript can safely represent without loss of precision:
    /// 2⁵³ − 1.
    /// </summary>
    private const long MaxJavascriptSafeInteger = 9_007_199_254_740_991L;
    
    /// <summary>
    /// The minimum integer value that JavaScript can safely represent without loss of precision:
    /// −(2⁵³ − 1).
    /// </summary>
    private const long MinJavascriptSafeInteger = -9_007_199_254_740_991L;

    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt64();
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Unexpected token parsing Int64. Expected Number or String, got {reader.TokenType}.");

        var stringValue = reader.GetString();
        if (long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            return result;
        
        throw new JsonException($"Cannot convert \"{stringValue}\" to Int64.");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        // If outside JS safe range, serialize as string to avoid precision loss
        //https://spec.openapis.org/registry/format/int64
        //https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Number
        if (value > IntegerSafeIntervalForJs.Max || value < IntegerSafeIntervalForJs.Min)
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
        else
            writer.WriteNumberValue(value);
    }
}