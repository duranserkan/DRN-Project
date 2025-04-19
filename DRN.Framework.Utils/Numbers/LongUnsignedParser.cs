namespace DRN.Framework.Utils.Numbers;

public class LongUnsignedParser(ulong value, NumberBuildDirection direction) : NumberParserUnsignedBase(direction, 64, value)
{
    public static LongUnsignedParser Default(ulong value) => new(value, NumberBuildDirection.MostSignificantFirst);
}