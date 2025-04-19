using DRN.Framework.Utils.Numbers;
using FluentAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class IntUnsignedBuilderMixedTests
{
    [Fact]
    public void IntBuilder_Should_Build_From_UShort_Value()
    {
        ushort expectedValue = 44_210;
        var intBuilder = IntUnsignedBuilder.Default;
        intBuilder.TryAddUShort(expectedValue);

        var intValue = intBuilder.GetValue();
        var intParser = IntUnsignedParser.Default(intValue);

        var actualValue = intParser.ReadUShort();
        actualValue.Should().Be(expectedValue);
    }
    
    [Fact]
    public void IntBuilder_Should_Build_From_Byte_Value()
    {
        byte expectedValue = 78;
        var intBuilder = IntUnsignedBuilder.Default;
        intBuilder.TryAddByte(expectedValue);

        var intValue = intBuilder.GetValue();

        var intParser = IntUnsignedParser.Default(intValue);

        var actualValue = intParser.ReadByte();
        actualValue.Should().Be(expectedValue);
    }
    
    [Fact]
    public void IntBuilder_Should_Build_From_Crumb_Value()
    {
        byte expectedValue = 2;
        var intBuilder = IntUnsignedBuilder.Default;
        intBuilder.TryAddCrumb(expectedValue);

        var intValue = intBuilder.GetValue();

        var intParser = IntUnsignedParser.Default(intValue);

        var actualValue = intParser.ReadCrumb();
        actualValue.Should().Be(expectedValue);
    }
    
    [Fact]
    public void IntBuilder_Should_Build_From_UShort_And_Bit_Values()
    {
        ushort expectedValue1 = 60_421;
        byte expectedValue2 = 103;
        byte expectedValue3 = 99;

        var intBuilder = IntUnsignedBuilder.Default;
        intBuilder.TryAddUShort(expectedValue1);
        intBuilder.TryAddByte(expectedValue2);
        intBuilder.TryAddByte(expectedValue3);

        var intValue = intBuilder.GetValue();

        var intParser = IntUnsignedParser.Default(intValue);

        var actualValue1 = intParser.ReadUShort();
        var actualValue2 = intParser.ReadByte();
        var actualValue3 = intParser.ReadByte();

        actualValue1.Should().Be(expectedValue1);
        actualValue2.Should().Be(expectedValue2);
        actualValue3.Should().Be(expectedValue3);
    }
}