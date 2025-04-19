namespace DRN.Framework.Utils.Numbers;

public class LongParser(long value, NumberBuildDirection direction, byte residueBitLength) : NumberParserSignedBase(direction, 64, residueBitLength, value)
{
    public static LongParser Default(long value) => new(value, NumberBuildDirection.MostSignificantFirst, 31);
}