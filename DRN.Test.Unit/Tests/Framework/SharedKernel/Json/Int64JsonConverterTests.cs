using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Testing.DataAttributes;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Json;

public class Int64JsonConverterTests
{
    private static readonly JsonSerializerOptions NonNullableOptions = new()
    {
        Converters = { new Int64ToStringConverter() }
    };

    private static readonly JsonSerializerOptions NullableOptions = new()
    {
        Converters = { new Int64NullableToStringConverter() }
    };

    [Theory]
    [DataInlineUnit(0L)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Min)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Max)]
    public void Write_Should_Serialize_Safe_Interval_Values_As_Json_Number(long value)
    {
        var jsonNonNullable = JsonSerializer.Serialize(value, NonNullableOptions);
        var nodeNonNullable = JsonNode.Parse(jsonNonNullable)!;
        nodeNonNullable.GetValueKind().Should().Be(JsonValueKind.Number);
        nodeNonNullable.GetValue<long>().Should().Be(value);

        long? nullableValue = value;
        var jsonNullable = JsonSerializer.Serialize(nullableValue, NullableOptions);
        var nodeNullable = JsonNode.Parse(jsonNullable)!;
        nodeNullable.GetValueKind().Should().Be(JsonValueKind.Number);
        nodeNullable.GetValue<long>().Should().Be(value);
    }

    [Theory]
    [DataInlineUnit(IntegerSafeIntervalForJs.Min - 1)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Max + 1)]
    [DataInlineUnit(long.MinValue)]
    [DataInlineUnit(long.MaxValue)]
    public void Write_Should_Serialize_Unsafe_Interval_Values_As_Json_String(long value)
    {
        var expected = value.ToString(CultureInfo.InvariantCulture);

        var jsonNonNullable = JsonSerializer.Serialize(value, NonNullableOptions);
        var nodeNonNullable = JsonNode.Parse(jsonNonNullable)!;
        nodeNonNullable.GetValueKind().Should().Be(JsonValueKind.String);
        nodeNonNullable.GetValue<string>().Should().Be(expected);

        long? nullableValue = value;
        var jsonNullable = JsonSerializer.Serialize(nullableValue, NullableOptions);
        var nodeNullable = JsonNode.Parse(jsonNullable)!;
        nodeNullable.GetValueKind().Should().Be(JsonValueKind.String);
        nodeNullable.GetValue<string>().Should().Be(expected);
    }

    [Fact]
    public void Write_Should_Serialize_Nullable_Null_As_Json_Null()
    {
        long? value = null;
        var json = JsonSerializer.Serialize(value, NullableOptions);
        json.Should().Be("null");

        var node = JsonNode.Parse(json);
        node.Should().BeNull();
    }

    [Theory]
    [DataInlineUnit("0", 0L)]
    [DataInlineUnit("-9007199254740991", IntegerSafeIntervalForJs.Min)]
    [DataInlineUnit("9007199254740991", IntegerSafeIntervalForJs.Max)]
    [DataInlineUnit("-9007199254740992", IntegerSafeIntervalForJs.Min - 1)]
    [DataInlineUnit("9007199254740992", IntegerSafeIntervalForJs.Max + 1)]
    [DataInlineUnit("12345", 12345L)]
    [DataInlineUnit("-12345", -12345L)]
    public void Read_Should_Deserialize_Numeric_Tokens(string json, long expected)
    {
        var resultNonNullable = JsonSerializer.Deserialize<long>(json, NonNullableOptions);
        resultNonNullable.Should().Be(expected);

        var resultNullable = JsonSerializer.Deserialize<long?>(json, NullableOptions);
        resultNullable.Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit("\"0\"", 0L)]
    [DataInlineUnit("\"-9007199254740991\"", IntegerSafeIntervalForJs.Min)]
    [DataInlineUnit("\"9007199254740991\"", IntegerSafeIntervalForJs.Max)]
    [DataInlineUnit("\"-9007199254740992\"", IntegerSafeIntervalForJs.Min - 1)]
    [DataInlineUnit("\"9007199254740992\"", IntegerSafeIntervalForJs.Max + 1)]
    [DataInlineUnit("\"-9223372036854775808\"", long.MinValue)]
    [DataInlineUnit("\"9223372036854775807\"", long.MaxValue)]
    [DataInlineUnit("\"12345\"", 12345L)]
    public void Read_Should_Deserialize_Quoted_Integer_Strings(string json, long expected)
    {
        var resultNonNullable = JsonSerializer.Deserialize<long>(json, NonNullableOptions);
        resultNonNullable.Should().Be(expected);

        var resultNullable = JsonSerializer.Deserialize<long?>(json, NullableOptions);
        resultNullable.Should().Be(expected);
    }

    [Fact]
    public void Read_Should_Deserialize_Nullable_Null_Token()
    {
        var result = JsonSerializer.Deserialize<long?>("null", NullableOptions);
        result.Should().BeNull();
    }

    [Theory]
    [DataInlineUnit(0L)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Min)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Max)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Min - 1)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Max + 1)]
    [DataInlineUnit(long.MinValue)]
    [DataInlineUnit(long.MaxValue)]
    public void RoundTrip_Should_Preserve_Values(long originalValue)
    {
        var serialized = JsonSerializer.Serialize(originalValue, NonNullableOptions);
        var deserialized = JsonSerializer.Deserialize<long>(serialized, NonNullableOptions);
        deserialized.Should().Be(originalValue);

        long? nullableOriginal = originalValue;
        var serializedNullable = JsonSerializer.Serialize(nullableOriginal, NullableOptions);
        var deserializedNullable = JsonSerializer.Deserialize<long?>(serializedNullable, NullableOptions);
        deserializedNullable.Should().Be(originalValue);
    }

    [Fact]
    public void RoundTrip_Should_Preserve_Nullable_Null()
    {
        long? value = null;
        var serialized = JsonSerializer.Serialize(value, NullableOptions);
        var deserialized = JsonSerializer.Deserialize<long?>(serialized, NullableOptions);
        deserialized.Should().BeNull();
    }

    [Theory]
    [DataInlineUnit("\"9223372036854775808\"")]
    [DataInlineUnit("\"-9223372036854775809\"")]
    [DataInlineUnit("\"\"")]
    [DataInlineUnit("\"abc\"")]
    [DataInlineUnit("\"123a\"")]
    [DataInlineUnit("\"   \"")]
    [DataInlineUnit("\"12.34\"")]
    public void Read_Should_Throw_JsonException_For_Invalid_String_Inputs(string json)
    {
        var actNonNullable = () => JsonSerializer.Deserialize<long>(json, NonNullableOptions);
        actNonNullable.Should().Throw<JsonException>();

        var actNullable = () => JsonSerializer.Deserialize<long?>(json, NullableOptions);
        actNullable.Should().Throw<JsonException>();
    }

    [Theory]
    [DataInlineUnit("12.34")]
    [DataInlineUnit("true")]
    [DataInlineUnit("false")]
    [DataInlineUnit("{}")]
    [DataInlineUnit("[]")]
    [DataInlineUnit("18446744073709551616")]
    public void Read_Should_Throw_JsonException_For_Invalid_Tokens_Or_Numbers(string json)
    {
        var actNonNullable = () => JsonSerializer.Deserialize<long>(json, NonNullableOptions);
        actNonNullable.Should().Throw<JsonException>();

        var actNullable = () => JsonSerializer.Deserialize<long?>(json, NullableOptions);
        actNullable.Should().Throw<JsonException>();
    }

    [Fact]
    public void Read_Should_Throw_JsonException_When_NonNullable_Converter_Receives_Null()
    {
        var act = () => JsonSerializer.Deserialize<long>("null", NonNullableOptions);
        act.Should().Throw<JsonException>();
    }
}
