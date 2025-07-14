namespace DRN.Framework.Utils.Numbers;

public class IntUnsignedParser(uint value, NumberBuildDirection direction) : NumberParserBase(direction, 32, 0, false, unsignedValue: value)
{
    public static IntUnsignedParser Default(uint value) => new(value, NumberBuildDirection.MostSignificantFirst);
}