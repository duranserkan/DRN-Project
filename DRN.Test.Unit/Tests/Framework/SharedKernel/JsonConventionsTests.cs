using System.Text.Json;
using System.Text.Json.Nodes;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.Data.Serialization;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel;

public class JsonConventionsTests
{
    [Fact]
    public void JsonSerializer_Should_Use_Updated_Conventions()
    {
        JsonConventions.DefaultOptions.Should().Be(JsonSerializerOptions.Default );
        var payload = $$"""
                        {
                        "environment":"Production", 
                        "MaxValue":{{long.MaxValue}},
                        "MaxValueQuoted":"{{long.MaxValue}}",
                        "MinValue":{{long.MinValue}},
                        "MinValueQuoted":"{{long.MinValue}}",
                        "ZeroValue": 0,
                        "ZeroValueQuoted": "0",
                        }
                        """;

        var testModel = payload.Deserialize<TestModel>()!;
        testModel.Environment.Should().Be(AppEnvironment.Production);
        testModel.MaxValue.Should().Be(long.MaxValue);
        testModel.MaxValueQuoted.Should().Be(long.MaxValue);
        testModel.MinValue.Should().Be(long.MinValue);
        testModel.MinValueQuoted.Should().Be(long.MinValue);
        testModel.ZeroValue.Should().Be(0);
        testModel.ZeroValueQuoted.Should().Be(0);
        testModel.NullValue.Should().BeNull();

        var payload2 = testModel.Serialize();
        var testModel2 = payload2.Deserialize<TestModel>()!;

        testModel2.Should().BeEquivalentTo(testModel);

        var jsonNode2 = JsonNode.Parse(payload2)!;
        jsonNode2["maxValue"]!.GetValueKind().Should().Be(JsonValueKind.String);
        jsonNode2["zeroValue"]!.GetValueKind().Should().Be(JsonValueKind.Number);
        jsonNode2["nullValue"].Should().BeNull();
    }


    public class TestModel
    {
        public AppEnvironment Environment { get; set; }
        public long MaxValue { get; set; }
        public long MaxValueQuoted { get; set; }
        public long MinValue { get; set; }
        public long MinValueQuoted { get; set; }
        public long ZeroValue { get; set; }
        public long ZeroValueQuoted { get; set; }
        public long? NullValue { get; set; }
    }
}