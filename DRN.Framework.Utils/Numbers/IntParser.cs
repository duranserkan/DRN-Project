namespace DRN.Framework.Utils.Numbers;

public class IntParser(int value, NumberBuildDirection direction, byte residueBitLength) : NumberParserBase(direction, 32, residueBitLength, true, value)
{
    public static IntParser Default(int value) => new(value, NumberBuildDirection.MostSignificantFirst, 15);
}