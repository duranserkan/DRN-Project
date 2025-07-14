namespace DRN.Framework.Utils.Numbers;

public class IntUnsignedBuilder(NumberBuildDirection direction) : NumberBuilderBase(direction, 32, 0, false)
{
    public static IntUnsignedBuilder Default => new(NumberBuildDirection.MostSignificantFirst);

    public uint GetValue() => (uint)UnsignedValue;
}