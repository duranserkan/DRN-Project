using DRN.Framework.Utils.Numbers;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class LongBuilderBitTests
{
    private const byte AvailableBits = 63;

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, true)]
    [InlineData(NumberBuildDirection.MostSignificantFirst, false)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, true)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, false)]
    public void LongBuilder_Should_Build_Max_With_Bits_Without_Residue(NumberBuildDirection direction, bool setResidue)
    {
        var maxAvailable = long.MaxValue;
        var builder = new LongBuilder(direction, 0);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddBit(1);

        if (setResidue)
            builder.SetResidueValue(0);

        builder.MakePositive();

        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(maxAvailable);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, true)]
    [InlineData(NumberBuildDirection.MostSignificantFirst, false)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, true)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, false)]
    public void LongBuilder_Should_Build_Zero_With_Bits(NumberBuildDirection direction, bool setResidue)
    {
        var builder = new LongBuilder(direction, 0);
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
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0x4000_0000_0000_0000)] // Mask for first MSB including sign bit
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 1)] // Mask for first bit
    public void LongBuilder_Should_Build_First_1_Significant_Bit(NumberBuildDirection direction, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = new LongBuilder(direction, 0);
        builder.TryAddBit(1);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0x6000_0000_0000_0000)] // Mask for first 2 MSBs
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 3)] // Mask for first 2 LSBs
    public void LongBuilder_Should_Build_First_2_Significant_Bits_With_Bytes(NumberBuildDirection direction, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = new LongBuilder(direction, 0);
        builder.TryAddBit(1);
        builder.TryAddBit(1);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_TryAddByte_Should_Return_False_When_All_Available_Slots_Filled_With_Bits(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, 0);
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
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Max_With_Bits(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, 0);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddBit(1);

        builder.SetResidueValue(0);

        builder.MakePositive();
        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(long.MaxValue);

        var parser = NumberParser.Get(actual, direction, 0);
        var residueValue = parser.ReadResidueValue();
        residueValue.Should().Be(0);

        var bits = Enumerable.Range(0, AvailableBits).Select(_ => parser.ReadBit()).ToArray();
        bits.Should().AllBeEquivalentTo(1);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Min_With_Bits(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, 0);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddByte(0);

        builder.SetResidueValue(0);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Negative_With_Max_Residue()
    {
        var builder = new LongBuilder(NumberBuildDirection.MostSignificantFirst, 0);
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
        var builder = new LongBuilder(NumberBuildDirection.MostSignificantFirst, 0);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddBit(1);

        builder.SetResidueValue(0);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().Be(-1);
    }
}