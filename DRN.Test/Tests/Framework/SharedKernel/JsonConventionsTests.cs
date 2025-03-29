using System.Text.Json;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.SharedKernel.Json;
using FluentAssertions;
using Xunit;

namespace DRN.Test.Tests.Framework.SharedKernel;

public class JsonConventionsTests
{
    [Fact]
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