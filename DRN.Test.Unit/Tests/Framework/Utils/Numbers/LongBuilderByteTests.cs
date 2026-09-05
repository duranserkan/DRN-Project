using DRN.Framework.Utils.Numbers;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class LongBuilderByteTests
{
    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, true)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, false)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, true)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, false)]
    public void LongBuilder_Should_Build_Max_With_Bytes_Without_Residue(NumberBuildDirection direction, bool setResidue)
    {
        var maxAvailable = 0x00FF_FFFF_FFFF_FFFF;
        var builder = NumberBuilder.GetLong(direction, 7);
        foreach (var _ in Enumerable.Range(0, 7))
            builder.TryAddByte(byte.MaxValue);

        if (setResidue)
            builder.SetResidueValue(0);

        builder.MakePositive();

        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(maxAvailable);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, true)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, false)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, true)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, false)]
    public void LongBuilder_Should_Build_Zero_With_Bytes(NumberBuildDirection direction, bool setResidue)
    {
        var builder = NumberBuilder.GetLong(direction, 7);
        foreach (var _ in Enumerable.Range(0, 7))
            builder.TryAddByte(0);

        if (setResidue)
            builder.SetResidueValue(0);

        builder.MakePositive();

        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(0);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 1, 0x00FF_0000_0000_0000)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 1, 255L)]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst, 2, 0x00FF_FF00_0000_0000)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst, 2, 65535L)]
    public void LongBuilder_Should_Position_Leading_Bytes(NumberBuildDirection direction, int count, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = NumberBuilder.GetLong(direction, 7);
        for (var index = 0; index < count; index++)
            builder.TryAddByte(byte.MaxValue).Should().BeTrue();

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_TryAddByte_Should_Return_False_When_All_Available_Slots_Filled_With_Bytes(NumberBuildDirection direction)
    {
        var builder = NumberBuilder.GetLong(direction, 7);
        var added = false;
        foreach (var _ in Enumerable.Range(0, 7))
            added = builder.TryAddByte(0);

        added.Should().BeTrue();

        builder.TryAddByte(0).Should().BeFalse();

        builder.Reset();
        builder.TryAddByte(15).Should().BeTrue();
        builder.GetValue().Should().BeGreaterThan(long.MinValue);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Max_With_Bytes(NumberBuildDirection direction)
    {
        var builder = NumberBuilder.GetLong(direction, 7);
        foreach (var _ in Enumerable.Range(0, 7))
            builder.TryAddByte(byte.MaxValue);

        builder.SetResidueValue(127);

        builder.MakePositive();
        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(long.MaxValue);

        var parser = NumberParser.Get(actual, direction, 7);
        var residueValue = parser.ReadResidueValue();
        residueValue.Should().Be(127);

        var bytes = Enumerable.Range(0, 7).Select(_ => parser.ReadByte()).ToArray();
        bytes.Should().AllBeEquivalentTo(255);
    }

    [Theory]
    [DataInlineUnit(NumberBuildDirection.MostSignificantFirst)]
    [DataInlineUnit(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Min_With_Bytes(NumberBuildDirection direction)
    {
        var builder = NumberBuilder.GetLong(direction, 7);
        foreach (var _ in Enumerable.Range(0, 7))
            builder.TryAddByte(0);

        builder.SetResidueValue(0);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Negative_With_Max_Residue()
    {
        var builder = NumberBuilder.GetLong(NumberBuildDirection.MostSignificantFirst, 7);
        foreach (var _ in Enumerable.Range(0, 7))
            builder.TryAddByte(0);

        builder.SetResidueValue(127);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().BeGreaterThan(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Minus_One()
    {
        var builder = NumberBuilder.GetLong(NumberBuildDirection.MostSignificantFirst, 7);
        foreach (var _ in Enumerable.Range(0, 7))
            builder.TryAddByte(byte.MaxValue);

        builder.SetResidueValue(127);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().Be(-1);
    }
}
