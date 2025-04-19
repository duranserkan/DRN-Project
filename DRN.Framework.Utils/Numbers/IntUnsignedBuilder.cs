namespace DRN.Framework.Utils.Numbers;

public class IntUnsignedBuilder(NumberBuildDirection direction) : NumberBuilderUnsignedBase(direction, 32)
{
    public static IntUnsignedBuilder Default => new(NumberBuildDirection.MostSignificantFirst);

    public uint GetValue() => (uint)UnsignedValue;
}