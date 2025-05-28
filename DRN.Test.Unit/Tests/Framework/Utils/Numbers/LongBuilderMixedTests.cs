using DRN.Framework.Utils.Numbers;
using AwesomeAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class LongBuilderMixedTests
{
    [Fact]
    public void LongBuilder_Should_Build_From_UInt_Residue()
    {
        uint expectedResidue = 7756421;

        var longBuilder = LongBuilder.Default;
        longBuilder.SetResidueValue(expectedResidue);
        var longValue = longBuilder.GetValue();

        var longParser = LongParser.Default(longValue);
        var actualResidue = longParser.ReadResidueValue();
        actualResidue.Should().Be(expectedResidue);
    }

    [Fact]
    public void LongBuilder_Should_Build_From_UInt_Residue_And_Nibble_Value()
    {
        uint expectedResidue = 7756421;
        byte expectedByte1 = 1;

        var longBuilder = LongBuilder.Default;
        longBuilder.SetResidueValue(expectedResidue);
        longBuilder.TryAddNibble(expectedByte1);
        var longValue = longBuilder.GetValue();

        var longParser = LongParser.Default(longValue);
        var actualResidue = longParser.ReadResidueValue();
        actualResidue.Should().Be(expectedResidue);

        var actualByte1 = longParser.ReadNibble();
        actualByte1.Should().Be(expectedByte1);
    }

    [Fact]
    public void LongBuilder_Should_Build_From_UInt_Residue_And_Byte_Value()
    {
        uint expectedResidue = 7756421;
        byte expectedByte1 = 1;

        var longBuilder = LongBuilder.Default;
        longBuilder.SetResidueValue(expectedResidue);
        longBuilder.TryAddByte(expectedByte1);
        var longValue = longBuilder.GetValue();

        var longParser = LongParser.Default(longValue);
        var actualResidue = longParser.ReadResidueValue();
        actualResidue.Should().Be(expectedResidue);

        var actualByte1 = longParser.ReadByte();
        actualByte1.Should().Be(expectedByte1);
    }

    [Fact]
    public void LongBuilder_Should_Build_From_UInt_Residue_And_Byte_Values()
    {
        uint expectedResidue = 7756421;
        byte expectedByte1 = 1;
        byte expectedByte2 = 2;

        var longBuilder = LongBuilder.Default;
        longBuilder.SetResidueValue(expectedResidue);
        longBuilder.TryAddByte(expectedByte1);
        longBuilder.TryAddByte(expectedByte2);
        var longValue = longBuilder.GetValue();

        var longParser = LongParser.Default(longValue);
        var actualResidue = longParser.ReadResidueValue();
        actualResidue.Should().Be(expectedResidue);

        var actualByte1 = longParser.ReadByte();
        actualByte1.Should().Be(expectedByte1);
        var actualByte2 = longParser.ReadByte();
        actualByte2.Should().Be(expectedByte2);
    }

    [Theory]
    [InlineData(7756421, 5, 130, 60001)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 1, 1, 1)]
    [InlineData(int.MaxValue, byte.MaxValue, byte.MaxValue, ushort.MaxValue)]
    public void LongBuilder_Should_Build_From_Mixed_Numbers(
        uint expectedResidue, byte expectedByte1, byte expectedByte2, ushort expectedUShort)
    {
        var longBuilder = LongBuilder.Default;
        longBuilder.SetResidueValue(expectedResidue);
        longBuilder.TryAddByte(expectedByte1);
        longBuilder.TryAddByte(expectedByte2);
        longBuilder.TryAddUShort(expectedUShort);
        var longValue = longBuilder.GetValue();

        var longParser = LongParser.Default(longValue);
        var actualResidue = longParser.ReadResidueValue();
        actualResidue.Should().Be(expectedResidue);

        var actualByte1 = longParser.ReadByte();
        actualByte1.Should().Be(expectedByte1);
        var actualByte2 = longParser.ReadByte();
        actualByte2.Should().Be(expectedByte2);
        var actualUShort = longParser.ReadUShort();
        actualUShort.Should().Be(expectedUShort);
    }
}