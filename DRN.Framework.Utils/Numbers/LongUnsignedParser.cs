namespace DRN.Framework.Utils.Numbers;

public class LongUnsignedParser(ulong value, NumberBuildDirection direction) : NumberParserBase(direction, 64, 0, false, unsignedValue: value)
{
    public static LongUnsignedParser Default(ulong value) => new(value, NumberBuildDirection.MostSignificantFirst);
}