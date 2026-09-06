using AwesomeAssertions;
using DRN.Framework.Utils.Numbers;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class LongUnsignedBuilderTests
{
    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Max(NumberBuildDirection direction)
    {
        var builder = NumberBuilder.GetLongUnsigned(direction);
        var nibbles = Enumerable.Range(0, 16).ToArray();
        foreach (var _ in nibbles)
            builder.TryAddNibble(15);

        var actual = builder.GetValue();
        actual.Should().Be(ulong.MaxValue);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Min(NumberBuildDirection direction)
    {
        var builder = NumberBuilder.GetLongUnsigned(direction);
        var nibbles = Enumerable.Range(0, 16).ToArray();
        foreach (var _ in nibbles)
            builder.TryAddNibble(0);

        var actual = builder.GetValue();
        actual.Should().Be(ulong.MinValue);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 1, 0xF000000000000000UL)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 1, 0x000000000000000FUL)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 2, 0xFF00000000000000UL)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 2, 0x00000000000000FFUL)]
    public void LongBuilder_Should_Position_Leading_Nibbles(NumberBuildDirection direction, int count, ulong mask)
    {
        var expected = ulong.MaxValue & mask;

        var builder = NumberBuilder.GetLongUnsigned(direction);
        for (var index = 0; index < count; index++)
            builder.TryAddNibble(15).Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_AddNibble_Should_Return_False_When_All_Available_Slots_Filled(NumberBuildDirection direction)
    {
        var builder = NumberBuilder.GetLongUnsigned(direction);
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
