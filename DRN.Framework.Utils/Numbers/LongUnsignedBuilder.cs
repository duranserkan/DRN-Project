namespace DRN.Framework.Utils.Numbers;

public class LongUnsignedBuilder(NumberBuildDirection direction) : NumberBuilderBase(direction, 64, 0, false)
{
    public static LongUnsignedBuilder Default => new(NumberBuildDirection.MostSignificantFirst);

    public ulong GetValue() => UnsignedValue;
}