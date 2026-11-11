using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Json;

/// <summary>
/// Emits 64-bit integers as JSON numbers if within JS-safe range;
/// otherwise emits them as JSON strings to avoid precision loss.
/// </summary>
public class Int64ToStringConverter : JsonConverter<long>
{
    private const int StackBufferSize = 64;
    private const string InvalidIntegerMessage = "Expected a signed 64-bit integer.";
    private const string FormattingFailureMessage = "Unable to format a signed 64-bit integer.";

    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var result))
            return result;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException(InvalidIntegerMessage);

        var encodedLength = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
        if (encodedLength > StackBufferSize)
            return long.TryParse(reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                ? result
                : throw new JsonException(InvalidIntegerMessage);

        // The encoded byte length bounds the number of unescaped UTF-16 characters.
        Span<char> buffer = stackalloc char[StackBufferSize];
        var charsWritten = reader.CopyString(buffer);
        return long.TryParse(buffer[..charsWritten], NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
            ? result
            : throw new JsonException(InvalidIntegerMessage);
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        // If outside JS safe range, serialize as string to avoid precision loss
        //https://spec.openapis.org/registry/format/int64
        //https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Number
        if (value is <= IntegerSafeIntervalForJs.Max and >= IntegerSafeIntervalForJs.Min)
        {
            writer.WriteNumberValue(value);
            return;
        }

        // The sign and 19 decimal digits of long.MinValue fit in 20 UTF-8 bytes.
        Span<byte> buffer = stackalloc byte[20];
        if (value.TryFormat(buffer, out var bytesWritten, provider: CultureInfo.InvariantCulture))
            writer.WriteStringValue(buffer[..bytesWritten]);
        else
            throw new InvalidOperationException(FormattingFailureMessage);
    }
}
