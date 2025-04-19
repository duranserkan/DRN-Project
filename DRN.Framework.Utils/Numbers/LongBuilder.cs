namespace DRN.Framework.Utils.Numbers;

public class LongBuilder(NumberBuildDirection direction, byte residueBitLength) : NumberBuilderSignedBase(direction, residueBitLength, 64)
{
    public static LongBuilder Default => new(NumberBuildDirection.MostSignificantFirst, 31);

    public long GetValue() => SignedValue;
}