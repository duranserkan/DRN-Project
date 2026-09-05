using DRN.Framework.Utils.Numbers;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class LongBuilderBitTests
{
    private const byte AvailableBits = 63;

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, true)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, false)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, true)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, false)]
    public void LongBuilder_Should_Build_Max_With_Bits_Without_Residue(NumberBuildDirection direction, bool setResidue)
    {
        var maxAvailable = long.MaxValue;
        var builder = NumberBuilder.GetLong(direction, 0);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddBit(1);

        if (setResidue)
            builder.SetResidueValue(0);

        builder.MakePositive();

        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(maxAvailable);

        var parser = NumberParser.Get(actual, direction, 0);
        parser.ReadResidueValue().Should().Be(0);
        var bits = Enumerable.Range(0, AvailableBits).Select(_ => parser.ReadBit()).ToArray();
        bits.Should().AllBeEquivalentTo(1);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, true)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, false)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, true)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, false)]
    public void LongBuilder_Should_Build_Zero_With_Bits(NumberBuildDirection direction, bool setResidue)
    {
        var builder = NumberBuilder.GetLong(direction, 0);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddBit(0);

        if (setResidue)
            builder.SetResidueValue(0);

        builder.MakePositive();

        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(0);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 1, 0x4000_0000_0000_0000)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 1, 1L)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 2, 0x6000_0000_0000_0000)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 2, 3L)]
    public void LongBuilder_Should_Position_Leading_Bits(NumberBuildDirection direction, int count, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = NumberBuilder.GetLong(direction, 0);
        for (var index = 0; index < count; index++)
            builder.TryAddBit(1).Should().BeTrue();

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_TryAddBit_Should_Return_False_When_All_Available_Slots_Filled_With_Bits(NumberBuildDirection direction)
    {
        var builder = NumberBuilder.GetLong(direction, 0);
        var added = false;
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            added = builder.TryAddBit(0);

        added.Should().BeTrue();

        builder.TryAddBit(0).Should().BeFalse();

        builder.Reset();
        builder.TryAddBit(1).Should().BeTrue();
        builder.GetValue().Should().BeGreaterThan(long.MinValue);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Min_With_Bits(NumberBuildDirection direction)
    {
        var builder = NumberBuilder.GetLong(direction, 0);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddBit(0).Should().BeTrue();

        builder.SetResidueValue(0);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Ignore_Residue_Value_When_Residue_Width_Is_Zero()
    {
        var builder = NumberBuilder.GetLong(NumberBuildDirection.MostSignificantFirst, 0);
        foreach (var _ in Enumerable.Range(0, 7))
            builder.TryAddByte(0);

        builder.SetResidueValue(127);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().Be(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Minus_One()
    {
        var builder = NumberBuilder.GetLong(NumberBuildDirection.MostSignificantFirst, 0);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddBit(1);

        builder.SetResidueValue(0);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().Be(-1);
    }
}
