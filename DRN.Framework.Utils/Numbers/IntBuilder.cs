namespace DRN.Framework.Utils.Numbers;

public class IntBuilder(NumberBuildDirection direction, byte residueBitLength) : NumberBuilderBase(direction, 32, residueBitLength, true)
{
    public static IntBuilder Default => new(NumberBuildDirection.MostSignificantFirst, 15);

    public int GetValue() => (int)SignedValue;
}