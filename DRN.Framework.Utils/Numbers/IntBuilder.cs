namespace DRN.Framework.Utils.Numbers;

public class IntBuilder(NumberBuildDirection direction, byte residueBitLength) : NumberBuilderSignedBase(direction, residueBitLength, 32)
{
    public static IntBuilder Default => new(NumberBuildDirection.MostSignificantFirst, 15);

    public int GetValue() => (int)SignedValue;
}