using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Numbers;
using FluentAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Common.Numbers;

public class LongBuilderNibbleTests
{
    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, true)]
    [InlineData(NumberBuildDirection.MostSignificantFirst, false)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, true)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, false)]
    public void LongBuilder_Should_Build_Max_Without_Residue(NumberBuildDirection direction, bool setResidue)
    {
        var maxAvailable = 0x0FFF_FFFF_FFFF_FFFF;
        var builder = new LongBuilder(direction, ResidueType.Nibble);
        foreach (var _ in Enumerable.Range(0, 15))
            builder.TryAddNibble(15);

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
    public void LongBuilder_Should_Build_Zero(NumberBuildDirection direction, bool setResidue)
    {
        var builder = new LongBuilder(direction, ResidueType.Nibble);
        foreach (var _ in Enumerable.Range(0, 15))
            builder.TryAddNibble(0);

        if (setResidue)
            builder.SetResidueValue(0);

        builder.MakePositive();

        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0x0F00_0000_0000_0000)] // Mask for first 4 MSBs
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 15L)] // Mask for first 4 LSBs
    public void LongBuilder_Should_Build_First_4_Significant_Bits(NumberBuildDirection direction, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = new LongBuilder(direction, ResidueType.Nibble);
        builder.TryAddNibble(15);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0x0FF0_0000_0000_0000)] // Mask for first 8 MSBs
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 255L)] // Mask for first 8 LSBs
    public void LongBuilder_Should_Build_First_8_Significant_Bits(NumberBuildDirection direction, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = new LongBuilder(direction, ResidueType.Nibble);
        builder.TryAddNibble(15);
        builder.TryAddNibble(15);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_TryAddNibble_Should_Return_False_When_All_Available_Slots_Filled(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, ResidueType.Nibble);
        var added = false;
        foreach (var _ in Enumerable.Range(0, 15))
            added = builder.TryAddNibble(0);

        added.Should().BeTrue();

        builder.TryAddNibble(0).Should().BeFalse();

        builder.Reset();
        builder.TryAddNibble(15).Should().BeTrue();
        builder.GetValue().Should().BeGreaterThan(long.MinValue);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Max(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, ResidueType.Nibble);
        foreach (var _ in Enumerable.Range(0, 15))
            builder.TryAddNibble(15);

        builder.MakePositive();
        builder.SetResidueValue(7);
        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(long.MaxValue);

        var parser = new LongParser(actual, direction, ResidueType.Nibble);
        var residueValue = parser.ReadResidueValue();
        residueValue.Should().Be(7);

        var nibbles = Enumerable.Range(0, 15).Select(_ => parser.ReadNibble()).ToArray();
        nibbles.Should().AllBeEquivalentTo(15);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Min(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, ResidueType.Nibble);
        foreach (var _ in Enumerable.Range(0, 15))
            builder.TryAddNibble(0);

        builder.SetResidueValue(0);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Negative_With_Max_Residue()
    {
        var builder = new LongBuilder(NumberBuildDirection.MostSignificantFirst, ResidueType.Nibble);
        foreach (var _ in Enumerable.Range(0, 15))
            builder.TryAddNibble(0);

        builder.SetResidueValue(7);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().BeGreaterThan(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Minus_One()
    {
        var builder = new LongBuilder(NumberBuildDirection.MostSignificantFirst, ResidueType.Nibble);
        foreach (var _ in Enumerable.Range(0, 15))
            builder.TryAddNibble(15);

        builder.SetResidueValue(7);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().Be(-1);
    }

    [Fact]
    public void Long_BitPositions()
    {
        0L.GetBitPositions().Ones.Count.Should().Be(0);
        (-1L).GetBitPositions().Zeros.Count.Should().Be(0);
        1L.GetBitPositions().Ones.Count.Should().Be(1);
        long.MaxValue.GetBitPositions().Zeros.Count.Should().Be(1);
        long.MinValue.GetBitPositions().Ones.Count.Should().Be(1);
    }
}