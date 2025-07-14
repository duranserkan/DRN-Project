using DRN.Framework.Utils.Numbers;
using AwesomeAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class IntBuilderMixedTests
{
    [Fact]
    public void IntBuilder_Should_Build_From_UInt_Residue()
    {
        uint expectedResidue = 31092;

        var intBuilder = NumberBuilder.GetInt();
        intBuilder.SetResidueValue(expectedResidue);
        var intValue = intBuilder.GetValue();

        var intParser = NumberParser.Get(intValue);
        var actualResidue = intParser.ReadResidueValue();
        actualResidue.Should().Be(expectedResidue);
    }

    [Fact]
    public void IntBuilder_Should_Build_From_UInt_Residue_And_Nibble_Value()
    {
        ushort expectedResidue = 31092;
        byte expectedByte1 = 1;

        var intBuilder = NumberBuilder.GetInt();
        intBuilder.SetResidueValue(expectedResidue);
        intBuilder.TryAddNibble(expectedByte1);
        var intValue = intBuilder.GetValue();

        var intParser = NumberParser.Get(intValue);
        var actualResidue = intParser.ReadResidueValue();
        actualResidue.Should().Be(expectedResidue);

        var actualByte1 = intParser.ReadNibble();
        actualByte1.Should().Be(expectedByte1);
    }

    [Fact]
    public void IntBuilder_Should_Build_From_UInt_Residue_And_Byte_Value()
    {
        ushort expectedResidue = 31092;
        byte expectedByte1 = 1;

        var intBuilder = NumberBuilder.GetInt();
        intBuilder.SetResidueValue(expectedResidue);
        intBuilder.TryAddByte(expectedByte1);
        var intValue = intBuilder.GetValue();

        var intParser = NumberParser.Get(intValue);
        var actualResidue = intParser.ReadResidueValue();
        actualResidue.Should().Be(expectedResidue);

        var actualByte1 = intParser.ReadByte();
        actualByte1.Should().Be(expectedByte1);
    }

    [Fact]
    public void IntBuilder_Should_Build_From_UInt_Residue_And_Byte_Values()
    {
        ushort expectedResidue = 31092;
        byte expectedByte1 = 1;
        byte expectedByte2 = 2;

        var intBuilder = NumberBuilder.GetInt();
        intBuilder.SetResidueValue(expectedResidue);
        intBuilder.TryAddByte(expectedByte1);
        intBuilder.TryAddByte(expectedByte2);
        var intValue = intBuilder.GetValue();

        var intParser = NumberParser.Get(intValue);
        var actualResidue = intParser.ReadResidueValue();
        actualResidue.Should().Be(expectedResidue);

        var actualByte1 = intParser.ReadByte();
        actualByte1.Should().Be(expectedByte1);
        var actualByte2 = intParser.ReadByte();
        actualByte2.Should().Be(expectedByte2);
    }
}