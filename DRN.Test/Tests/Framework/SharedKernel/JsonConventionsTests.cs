using System.Text.Json;
using DRN.Framework.SharedKernel.Conventions;
using DRN.Framework.SharedKernel.Enums;

namespace DRN.Test.Tests.Framework.SharedKernel;

public class JsonConventionsTests
{
    [Theory]
    [DataInline]
    public void JsonSerializer_Should_Use_Updated_Conventions()
    {
        JsonSerializerOptions.Default.Should().Be(JsonConventions.DefaultOptions);
        var enumTest = JsonSerializer.Deserialize<EnumTest>(@"{""environment"":""Production""}")!;
        enumTest.Environment.Should().Be(AppEnvironment.Production);
    }
    
    public class EnumTest
    {
        public AppEnvironment Environment { get; set; }
    }
}