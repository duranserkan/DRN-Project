using System.Text.Json;
using System.Text.Json.Serialization;
using DRN.Framework.Utils.Logging.Diagnostics;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public sealed class DiagnosticJsonSizeTests
{
    [Fact]
    public void Size_Should_Match_Json_For_Escapes_Unicode_Growth_And_Reused_Writers()
    {
        using var sizing = DiagnosticJsonSize.Rent();
        var texts = new[] { "", "plain", "\"\\\r\n\t\0<>&", "Türkçe 😀", "\uD800", "\uDC00", new string('界', 10000), "tail" };
        foreach (var text in texts)
        {
            var expected = JsonSerializer.SerializeToUtf8Bytes(text).Length;
            sizing.Measure(text).Should().Be(expected);
            sizing.MeasureString(text).Should().Be(expected);
        }
        var tree = new Dictionary<string, object?>
        {
            ["Data"] = texts,
            ["Number"] = 123.45m,
            ["Missing"] = null,
            ["Flags"] = new[] { true, false },
            ["Items"] = Enumerable.Range(0, 2000).Select(value => new[] { value, value + 1 }).ToArray()
        };
        sizing.Measure(tree).Should().Be(JsonSerializer.SerializeToUtf8Bytes(tree).Length);
        sizing.MeasureString("tail").Should().Be(6);
    }

    [Fact]
    public void Single_Item_List_Entry_Size_Should_Match_Serialized_Json()
    {
        using var sizing = DiagnosticJsonSize.Rent();
        object?[] values = [null, "", "\"\\\r\n<>& Türkçe 😀", 123.45m,
            new Dictionary<string, object?> { ["Nested"] = new[] { "first", "second" } }];
        const string key = "Items\"\\界";
        foreach (var value in values)
        {
            var entry = new Dictionary<string, object?> { [key] = new List<object?> { value } };
            // Replace the two object braces with the reserved entry delimiter.
            var expected = JsonSerializer.SerializeToUtf8Bytes(entry).Length - 1;
            sizing.MeasureSingleItemListEntry(key, sizing.Measure(value)).Should().Be(expected);
        }
    }

    [Fact]
    public void Nested_Rentals_And_Serialization_Failures_Should_Not_Corrupt_Counts()
    {
        using (var sizing = DiagnosticJsonSize.Rent())
        {
            var probe = new ReentrantProbe();
            sizing.Measure(probe).Should().Be(JsonSerializer.SerializeToUtf8Bytes(probe).Length);
            Action invalid = () => sizing.Measure(double.NaN);
            invalid.Should().Throw<ArgumentException>();
            sizing.Measure(42).Should().Be(2);
        }
        using var reused = DiagnosticJsonSize.Rent();
        reused.MeasureString("clean").Should().Be(7);
    }

    [JsonConverter(typeof(ReentrantConverter))]
    private sealed class ReentrantProbe;

    private sealed class ReentrantConverter : JsonConverter<ReentrantProbe>
    {
        public ReentrantConverter() { }

        public override ReentrantProbe Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, ReentrantProbe value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteStringValue("before");
            using (var nested = DiagnosticJsonSize.Rent())
                writer.WriteNumberValue(nested.MeasureString("nested"));
            writer.WriteStringValue("after");
            writer.WriteEndArray();
        }
    }
}
