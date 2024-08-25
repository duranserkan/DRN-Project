using System.Text.Json;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.SharedKernel.Json;

namespace DRN.Test.Tests.Framework.SharedKernel;

public class JsonConventionsTests
{
    [Theory]
    [DataInline]
    public void JsonSerializer_Should_Use_Updated_Conventions(TestContext _)
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