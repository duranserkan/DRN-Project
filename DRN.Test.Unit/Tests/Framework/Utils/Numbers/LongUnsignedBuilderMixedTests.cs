using DRN.Framework.Utils.Numbers;
using AwesomeAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class LongUnsignedBuilderMixedTests
{
    [Fact]
    public void LongBuilder_Should_Build_From_UInt_Value()
    {
        uint expectedValue = 1_564_210;
        var longBuilder = LongUnsignedBuilder.Default;
        longBuilder.TryAddUInt(expectedValue);

        var longValue = longBuilder.GetValue();
        var longParser = NumberParser.Get(longValue);

        var actualValue = longParser.ReadUInt();
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_From_UShort_Value()
    {
        ushort expectedValue = 56_421;
        var longBuilder = LongUnsignedBuilder.Default;
        longBuilder.TryAddUShort(expectedValue);

        var longValue = longBuilder.GetValue();

        var longParser = NumberParser.Get(longValue);

        var actualValue = longParser.ReadUShort();
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_From_Byte_Value()
    {
        byte expectedValue = 78;
        var longBuilder = LongUnsignedBuilder.Default;
        longBuilder.TryAddByte(expectedValue);

        var longValue = longBuilder.GetValue();

        var longParser = NumberParser.Get(longValue);

        var actualValue = longParser.ReadByte();
        actualValue.Should().Be(expectedValue);
    }
    
    [Fact]
    public void LongBuilder_Should_Build_From_Crumb_Value()
    {
        byte expectedValue = 2;
        var longBuilder = LongUnsignedBuilder.Default;
        longBuilder.TryAddCrumb(expectedValue);

        var longValue = longBuilder.GetValue();

        var longParser = NumberParser.Get(longValue);

        var actualValue = longParser.ReadCrumb();
        actualValue.Should().Be(expectedValue);
    }


    [Fact]
    public void LongBuilder_Should_Build_From_UShort_And_Bit_Values()
    {
        ushort expectedValue1 = 60_421;
        byte expectedValue2 = 1;

        var longBuilder = LongUnsignedBuilder.Default;
        longBuilder.TryAddUShort(expectedValue1);
        longBuilder.TryAddBit(expectedValue2);

        var longValue = longBuilder.GetValue();

        var longParser = NumberParser.Get(longValue);

        var actualValue1 = longParser.ReadUShort();
        var actualValue2 = longParser.ReadBit();

        actualValue1.Should().Be(expectedValue1);
        actualValue2.Should().Be(expectedValue2);
    }

    [Fact]
    public void LongBuilder_Should_Build_From_UInt_And_Byte_Values()
    {
        uint expectedValue1 = 11_160_421;
        byte expectedValue2 = 103;

        var longBuilder = LongUnsignedBuilder.Default;
        longBuilder.TryAddUInt(expectedValue1);
        longBuilder.TryAddByte(expectedValue2);

        var longValue = longBuilder.GetValue();

        var longParser = NumberParser.Get(longValue);

        var actualValue1 = longParser.ReadUInt();
        var actualValue2 = longParser.ReadByte();

        actualValue1.Should().Be(expectedValue1);
        actualValue2.Should().Be(expectedValue2);
    }

    [Fact]
    public void LongBuilder_Should_Build_From_UInt_UShort_And_Byte_Values()
    {
        uint expectedValue1 = 11_160_421;
        ushort expectedValue2 = 61_111;
        byte expectedValue3 = 103;

        var longBuilder = LongUnsignedBuilder.Default;
        longBuilder.TryAddUInt(expectedValue1);
        longBuilder.TryAddUShort(expectedValue2);
        longBuilder.TryAddByte(expectedValue3);

        var longValue = longBuilder.GetValue();

        var longParser = NumberParser.Get(longValue);

        var actualValue1 = longParser.ReadUInt();
        var actualValue2 = longParser.ReadUShort();
        var actualValue3 = longParser.ReadByte();

        actualValue1.Should().Be(expectedValue1);
        actualValue2.Should().Be(expectedValue2);
        actualValue3.Should().Be(expectedValue3);
    }

    [Theory]
    [InlineData(7_756_421, 5, 130, 60_001)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 1, 1, 1)]
    [InlineData(int.MaxValue, byte.MaxValue, byte.MaxValue, ushort.MaxValue)]
    public void LongBuilder_Should_Build_From_Mixed_Numbers(
        uint expectedUInt, byte expectedByte1, byte expectedByte2, ushort expectedUShort)
    {
        var longBuilder = LongUnsignedBuilder.Default;
        longBuilder.TryAddUInt(expectedUInt);
        longBuilder.TryAddByte(expectedByte1);
        longBuilder.TryAddByte(expectedByte2);
        longBuilder.TryAddUShort(expectedUShort);
        var longValue = longBuilder.GetValue();

        var longParser = NumberParser.Get(longValue);
        var actualUInt = longParser.ReadUInt();
        actualUInt.Should().Be(expectedUInt);

        var actualByte1 = longParser.ReadByte();
        actualByte1.Should().Be(expectedByte1);
        var actualByte2 = longParser.ReadByte();
        actualByte2.Should().Be(expectedByte2);
        var actualUShort = longParser.ReadUShort();
        actualUShort.Should().Be(expectedUShort);
    }
}