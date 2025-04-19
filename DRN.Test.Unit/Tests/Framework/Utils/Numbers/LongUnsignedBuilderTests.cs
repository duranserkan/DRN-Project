using DRN.Framework.Utils.Numbers;
using FluentAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class LongUnsignedBuilderTests
{
    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Max(NumberBuildDirection direction)
    {
        var builder = new LongUnsignedBuilder(direction);
        var nibbles = Enumerable.Range(0, 16).ToArray();
        foreach (var _ in nibbles)
            builder.TryAddNibble(15);

        var actual = builder.GetValue();
        actual.Should().Be(ulong.MaxValue);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Min(NumberBuildDirection direction)
    {
        var builder = new LongUnsignedBuilder(direction);
        var nibbles = Enumerable.Range(0, 16).ToArray();
        foreach (var _ in nibbles)
            builder.TryAddNibble(0);

        var actual = builder.GetValue();
        actual.Should().Be(ulong.MinValue);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0xF000000000000000)] // Mask for first 4 MSBs
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 0x000000000000000F)] // Mask for first 4 LSBs
    public void LongBuilder_Should_Build_First_4_Significant_Bits(NumberBuildDirection direction, ulong mask)
    {
        var expected = ulong.MaxValue & mask;

        var builder = new LongUnsignedBuilder(direction);
        builder.TryAddNibble(15);

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0xFF00000000000000)] // Mask for first 8 MSBs
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 0x00000000000000FF)] // Mask for first 8 LSBs
    public void LongBuilder_Should_Build_First_8_Significant_Bits(NumberBuildDirection direction, ulong mask)
    {
        var expected = ulong.MaxValue & mask;

        var builder = new LongUnsignedBuilder(direction);
        builder.TryAddNibble(15);
        builder.TryAddNibble(15);

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_AddNibble_Should_Return_False_When_All_Available_Slots_Filled(NumberBuildDirection direction)
    {
        var builder = new LongUnsignedBuilder(direction);
        var nibbles = Enumerable.Range(0, 16).ToArray();
        var added = false;
        foreach (var _ in nibbles)
            added = builder.TryAddNibble(0);

        added.Should().BeTrue();

        builder.TryAddNibble(0).Should().BeFalse();

        builder.Reset();
        builder.TryAddNibble(15).Should().BeTrue();
        builder.GetValue().Should().BeGreaterThan(0);
    }
}