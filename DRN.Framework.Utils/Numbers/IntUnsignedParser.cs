namespace DRN.Framework.Utils.Numbers;

public class IntUnsignedParser(uint value, NumberBuildDirection direction) : NumberParserUnsignedBase(direction, 32, value)
{
    public static IntUnsignedParser Default(uint value) => new(value, NumberBuildDirection.MostSignificantFirst);
}