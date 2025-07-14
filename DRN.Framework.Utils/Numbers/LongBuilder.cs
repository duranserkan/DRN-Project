namespace DRN.Framework.Utils.Numbers;

public class LongBuilder(NumberBuildDirection direction, byte residueBitLength) : NumberBuilderBase(direction, 64, residueBitLength, true)
{
    public static LongBuilder Default => new(NumberBuildDirection.MostSignificantFirst, 31);

    public long GetValue() => SignedValue;
}