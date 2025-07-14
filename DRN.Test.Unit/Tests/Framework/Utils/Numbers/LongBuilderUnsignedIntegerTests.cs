using DRN.Framework.Utils.Numbers;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class LongBuilderUnsignedIntegerTests
{
    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, true)]
    [InlineData(NumberBuildDirection.MostSignificantFirst, false)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, true)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, false)]
    public void LongBuilder_Should_Build_Max_With_Ints_Without_Residue(NumberBuildDirection direction, bool setResidue)
    {
        var maxAvailable = 0x0000_0000_FFFF_FFFF;
        var builder = new LongBuilder(direction, 31);

        builder.TryAddUInt(uint.MaxValue);

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
    public void LongBuilder_Should_Build_Zero_With_Ints(NumberBuildDirection direction, bool setResidue)
    {
        var builder = new LongBuilder(direction, 31);
        builder.TryAddUInt(0);

        if (setResidue)
            builder.SetResidueValue(0);

        builder.MakePositive();

        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0x0000_0000_FFFF_FFFF)] // Mask for first 4 MSBs
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 0x0000_0000_FFFF_FFFF)] // Mask for first 4 LSBs
    public void LongBuilder_Should_Build_First_32_Significant_Bits_With_Ints(NumberBuildDirection direction, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = new LongBuilder(direction, 31);
        builder.TryAddUInt(uint.MaxValue);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_TryAddByte_Should_Return_False_When_All_Available_Slots_Filled_With_Ints(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, 31);
        var added = builder.TryAddUInt(uint.MaxValue);

        added.Should().BeTrue();

        builder.TryAddUInt(0).Should().BeFalse();

        builder.Reset();
        builder.TryAddUInt(15).Should().BeTrue();
        builder.GetValue().Should().BeGreaterThan(long.MinValue);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Max_With_Ints(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, 31);
        builder.TryAddUInt(uint.MaxValue);

        builder.SetResidueValue(int.MaxValue);

        builder.MakePositive();
        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(long.MaxValue);

        var parser = NumberParser.Get(actual, direction, 31);
        var residueValue = parser.ReadResidueValue();
        residueValue.Should().Be(int.MaxValue);

        parser.ReadUInt().Should().Be(uint.MaxValue);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Min_With_Ints(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, 31);
        builder.TryAddUInt(0);

        builder.SetResidueValue(0);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Negative_With_Max_Residue()
    {
        var builder = new LongBuilder(NumberBuildDirection.MostSignificantFirst, 31);
        builder.TryAddUInt(0);

        builder.SetResidueValue(int.MaxValue);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().BeGreaterThan(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Minus_One()
    {
        var builder = new LongBuilder(NumberBuildDirection.MostSignificantFirst, 31);
        builder.TryAddUInt(uint.MaxValue);

        builder.SetResidueValue(int.MaxValue);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().Be(-1);
    }
}