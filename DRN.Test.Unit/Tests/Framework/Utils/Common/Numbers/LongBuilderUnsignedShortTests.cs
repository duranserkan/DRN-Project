using DRN.Framework.Utils.Common.Numbers;
using FluentAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Common.Numbers;

public class LongBuilderUnsignedShortTests
{
    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, true)]
    [InlineData(NumberBuildDirection.MostSignificantFirst, false)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, true)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, false)]
    public void LongBuilder_Should_Build_Max_With_Shorts_Without_Residue(NumberBuildDirection direction, bool setResidue)
    {
        var maxAvailable = 0x0000_FFFF_FFFF_FFFF;
        var builder = new LongBuilder(direction, ResidueType.UShort);
        foreach (var _ in Enumerable.Range(0, 3))
            builder.TryAddUnsignedShort(ushort.MaxValue);

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
    public void LongBuilder_Should_Build_Zero_With_Shorts(NumberBuildDirection direction, bool setResidue)
    {
        var builder = new LongBuilder(direction, ResidueType.UShort);
        foreach (var _ in Enumerable.Range(0, 3))
            builder.TryAddUnsignedShort(0);

        if (setResidue)
            builder.SetResidueValue(0);

        builder.MakePositive();
        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0x0000_FFFF_0000_0000)] // Mask for first 4 MSBs
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 65535L)] // Mask for first 4 LSBs
    public void LongBuilder_Should_Build_First_16_Significant_Bits_With_Shorts(NumberBuildDirection direction, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = new LongBuilder(direction, ResidueType.UShort);
        builder.TryAddUnsignedShort(ushort.MaxValue);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0x0000_FFFF_FFFF_0000)] // Mask for first 8 MSBs
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 0x0000_0000_FFFF_FFFF)] // Mask for first 8 LSBs
    public void LongBuilder_Should_Build_First_32_Significant_Bits_With_Shorts(NumberBuildDirection direction, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = new LongBuilder(direction, ResidueType.UShort);
        builder.TryAddUnsignedShort(ushort.MaxValue);
        builder.TryAddUnsignedShort(ushort.MaxValue);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_TryAddByte_Should_Return_False_When_All_Available_Slots_Filled_With_Shorts(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, ResidueType.UShort);
        var added = false;
        foreach (var _ in Enumerable.Range(0, 3))
            added = builder.TryAddUnsignedShort(0);

        added.Should().BeTrue();

        builder.TryAddUnsignedShort(0).Should().BeFalse();

        builder.Reset();
        builder.TryAddUnsignedShort(15).Should().BeTrue();
        builder.GetValue().Should().BeGreaterThan(long.MinValue);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Max_With_Shorts(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, ResidueType.UShort);
        foreach (var _ in Enumerable.Range(0, 3))
            builder.TryAddUnsignedShort(ushort.MaxValue);

        builder.SetResidueValue((ushort)short.MaxValue);

        builder.MakePositive();
        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(long.MaxValue);

        var parser = new LongParser(actual, direction, ResidueType.UShort);
        var residueValue = parser.ReadResidueValue();
        residueValue.Should().Be((uint)short.MaxValue);

        var shorts = Enumerable.Range(0, 3).Select(_ => parser.ReadUShort()).ToArray();
        shorts.Should().AllBeEquivalentTo(ushort.MaxValue);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Min_With_Shorts(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, ResidueType.UShort);
        foreach (var _ in Enumerable.Range(0, 3))
            builder.TryAddUnsignedShort(0);

        builder.SetResidueValue(0);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Negative_With_Max_Residue()
    {
        var builder = new LongBuilder(NumberBuildDirection.MostSignificantFirst, ResidueType.UShort);
        foreach (var _ in Enumerable.Range(0, 3))
            builder.TryAddUnsignedShort(0);

        builder.SetResidueValue((ushort)short.MaxValue);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().BeGreaterThan(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Minus_One()
    {
        var builder = new LongBuilder(NumberBuildDirection.MostSignificantFirst, ResidueType.UShort);
        foreach (var _ in Enumerable.Range(0, 3))
            builder.TryAddUnsignedShort(ushort.MaxValue);

        builder.SetResidueValue((ushort)short.MaxValue);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().Be(-1);
    }
}