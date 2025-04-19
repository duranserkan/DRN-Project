namespace DRN.Framework.Utils.Numbers;

public class LongUnsignedBuilder(NumberBuildDirection direction) : NumberBuilderUnsignedBase(direction, 64)
{
    public static LongUnsignedBuilder Default => new(NumberBuildDirection.MostSignificantFirst);
    
    public ulong GetValue() => UnsignedValue;
}