using DRN.Framework.Utils.Numbers;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class NumberParserResetTests
{
    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst)]
    public void ResetToParse_Long_Should_Reset_Cursor_And_Replace_Value_Preserving_Direction_And_Residue(NumberBuildDirection direction)
    {
        // 64-bit long parser with 32-bit residue => 31 available bits (64 - 32 - 1)
        var initialValue = 0x1234_5678_0000_0000L;
        var parser = NumberParser.Get(initialValue, direction, 32);

        // Read 8 bits to advance cursor
        _ = parser.ReadByte();

        var newValue = 0x5555_AAAA_0000_0000L;
        parser.ResetToParse(newValue);

        // Residue bit length is unchanged (32 bits)
        var expectedResidue = (uint)((newValue >> 31) & ((byte)32).GetBitMaskSigned());
        parser.ReadResidueValue().Should().Be(expectedResidue);

        // Verify cursor returned to beginning by re-reading first byte from position 0
        var expectedFirstByte = (byte)((newValue >> (direction == NumberBuildDirection.MostSignificantFirst ? 23 : 0)) & ((byte)8).GetBitMaskSigned());
        parser.ReadByte().Should().Be(expectedFirstByte);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 0L)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 1234567L)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, -1234567L)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, long.MinValue)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, long.MaxValue)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 0L)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 1234567L)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, -1234567L)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, long.MinValue)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, long.MaxValue)]
    public void ResetToParse_Long_Should_Support_Boundary_Values_And_Repeated_Resets(NumberBuildDirection direction, long resetValue)
    {
        var parser = NumberParser.Get(100L, direction, 16); // available bits = 64 - 16 - 1 = 47

        // Partial read
        _ = parser.ReadNibble();

        // First reset
        parser.ResetToParse(resetValue);
        var expectedFirstValue = direction == NumberBuildDirection.MostSignificantFirst
            ? (uint)((resetValue >> 31) & ((byte)16).GetBitMaskSigned())
            : (uint)(resetValue & ((byte)16).GetBitMaskSigned());
        parser.Read(16).Should().Be(expectedFirstValue);

        // Complete remaining available bits (47 - 16 = 31 bits)
        var expectedSecondValue = direction == NumberBuildDirection.MostSignificantFirst
            ? (uint)(resetValue & ((byte)31).GetBitMaskSigned())
            : (uint)((resetValue >> 16) & ((byte)31).GetBitMaskSigned());
        parser.Read(31).Should().Be(expectedSecondValue);

        // Reading beyond available width throws InvalidOperationException
        var actOverRead = () => parser.ReadBit();
        actOverRead.Should().ThrowExactly<InvalidOperationException>();

        // Second reset after complete read restores available bits
        parser.ResetToParse(resetValue);

        // Read all 47 available bits again and verify the reset value is still used
        parser.Read(16).Should().Be(expectedFirstValue);
        parser.Read(31).Should().Be(expectedSecondValue);

        // Overflow read throws again
        actOverRead.Should().ThrowExactly<InvalidOperationException>();
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst)]
    public void ResetToParse_ULong_Should_Reset_Cursor_And_Replace_Value_Preserving_Direction(NumberBuildDirection direction)
    {
        var initialValue = 0x1234_5678_9ABC_DEF0UL;
        var parser = NumberParser.Get(initialValue, direction); // 64 available bits

        _ = parser.ReadUInt();

        var newValue = 0xAAAA_BBBB_CCCC_DDDDUL;
        parser.ResetToParse(newValue);

        // Re-read 32 bits from start
        var shift = direction == NumberBuildDirection.MostSignificantFirst ? 32 : 0;
        var expectedFirstUInt = (uint)((newValue >> shift) & ((byte)32).GetBitMaskUnsigned());
        parser.ReadUInt().Should().Be(expectedFirstUInt);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 0UL)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 0x1234_5678_9ABC_DEF0UL)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 0xAAAA_AAAA_AAAA_AAAAUL)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, ulong.MaxValue)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 0UL)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 0x1234_5678_9ABC_DEF0UL)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 0xAAAA_AAAA_AAAA_AAAAUL)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, ulong.MaxValue)]
    public void ResetToParse_ULong_Should_Support_Bit_Patterns_Boundary_Values_And_Repeated_Resets(NumberBuildDirection direction, ulong resetValue)
    {
        var parser = NumberParser.Get(100UL, direction); // 64 available bits

        // Partial read
        _ = parser.ReadByte();

        // First reset
        parser.ResetToParse(resetValue);
        var expectedFirstValue = direction == NumberBuildDirection.MostSignificantFirst
            ? (uint)(resetValue >> 32)
            : (uint)resetValue;
        parser.Read(32).Should().Be(expectedFirstValue);

        // Complete remaining available bits (64 - 32 = 32 bits)
        var expectedSecondValue = direction == NumberBuildDirection.MostSignificantFirst
            ? (uint)resetValue
            : (uint)(resetValue >> 32);
        parser.Read(32).Should().Be(expectedSecondValue);

        // Reading beyond available width throws InvalidOperationException
        var actOverRead = () => parser.ReadBit();
        actOverRead.Should().ThrowExactly<InvalidOperationException>();

        // Second reset after complete read restores available bits
        parser.ResetToParse(resetValue);

        // Read all 64 available bits again and verify the reset value is still used
        parser.ReadUInt().Should().Be(expectedFirstValue);
        parser.ReadUInt().Should().Be(expectedSecondValue);

        // Overflow read throws again
        actOverRead.Should().ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public void ResetToParse_Should_Reject_Value_With_Incompatible_Signedness()
    {
        var signedParser = NumberParser.Get(0L);
        var unsignedParser = NumberParser.Get(0UL);

        var resetSignedParserWithUnsignedValue = () => signedParser.ResetToParse(1UL);
        var resetUnsignedParserWithSignedValue = () => unsignedParser.ResetToParse(1L);

        resetSignedParserWithUnsignedValue.Should().ThrowExactly<InvalidOperationException>();
        resetUnsignedParserWithSignedValue.Should().ThrowExactly<InvalidOperationException>();
    }
}
