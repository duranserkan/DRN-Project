using DRN.Framework.Utils.Numbers;

namespace DRN.Test.Unit.Tests.Framework.Utils.Numbers;

public class LongBuilderCrumbTests
{
    private const byte AvailableBits = 31;

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, true)]
    [InlineData(NumberBuildDirection.MostSignificantFirst, false)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, true)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst, false)]
    public void LongBuilder_Should_Build_Max_Without_Residue(NumberBuildDirection direction, bool setResidue)
    {
        var maxAvailable = 0x3FFF_FFFF_FFFF_FFFF; //1 bit reserved for sign, 1 bit is for the residue remaining 2 bit can build max 3
        var builder = new LongBuilder(direction, 1);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddCrumb(3);

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
        var builder = new LongBuilder(direction, 1);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddCrumb(0);

        if (setResidue)
            builder.SetResidueValue(0);

        builder.MakePositive();

        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0x3000_0000_0000_0000)] // Mask for first 2 MSBs
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 3)] // Mask for first 2 LSBs
    public void LongBuilder_Should_Build_First_2_Significant_Bits(NumberBuildDirection direction, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = new LongBuilder(direction, 1);
        builder.TryAddCrumb(3);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst, 0x3C00_0000_0000_0000)] // Mask for first 4 MSBs
    [InlineData(NumberBuildDirection.LeastSignificantFirst, 15)] // Mask for first 4 LSBs
    public void LongBuilder_Should_Build_First_4_Significant_Bits(NumberBuildDirection direction, long mask)
    {
        var expected = long.MinValue + (long.MaxValue & mask);

        var builder = new LongBuilder(direction, 1);
        builder.TryAddCrumb(3);
        builder.TryAddCrumb(3);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_TryAddCrumb_Should_Return_False_When_All_Available_Slots_Filled(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, 1);
        var added = false;
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            added = builder.TryAddCrumb(0);

        added.Should().BeTrue();

        builder.TryAddCrumb(0).Should().BeFalse();

        builder.Reset();
        builder.TryAddCrumb(3).Should().BeTrue();
        builder.GetValue().Should().BeGreaterThan(long.MinValue);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Max(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, 1);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddCrumb(3);

        builder.MakePositive();
        builder.SetResidueValue(1);
        builder.IsPositive().Should().BeTrue();

        var actual = builder.GetValue();
        actual.Should().Be(long.MaxValue);

        var parser = NumberParser.Get(actual, direction, 1);
        var residueValue = parser.ReadResidueValue();
        residueValue.Should().Be(1);

        var crumbs = Enumerable.Range(0, AvailableBits).Select(_ => parser.ReadCrumb()).ToArray();
        crumbs.Should().AllBeEquivalentTo(3);
    }

    [Theory]
    [InlineData(NumberBuildDirection.MostSignificantFirst)]
    [InlineData(NumberBuildDirection.LeastSignificantFirst)]
    public void LongBuilder_Should_Build_Min(NumberBuildDirection direction)
    {
        var builder = new LongBuilder(direction, 1);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddCrumb(0);

        builder.SetResidueValue(0);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();
        actual.Should().Be(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Negative_With_Max_Residue()
    {
        var builder = new LongBuilder(NumberBuildDirection.MostSignificantFirst, 1);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddCrumb(0);

        builder.SetResidueValue(1);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().BeGreaterThan(long.MinValue);
    }

    [Fact]
    public void LongBuilder_Should_Build_Minus_One()
    {
        var builder = new LongBuilder(NumberBuildDirection.MostSignificantFirst, 1);
        foreach (var _ in Enumerable.Range(0, AvailableBits))
            builder.TryAddCrumb(3);

        builder.SetResidueValue(1);

        builder.IsPositive().Should().BeFalse();

        var actual = builder.GetValue();

        actual.Should().BeNegative();
        actual.Should().Be(-1);
    }
}