namespace DRN.Framework.Utils.Numbers;


public class IntParser(int value, NumberBuildDirection direction, byte residueBitLength) : NumberParserSignedBase(direction, 32, residueBitLength, value)
{
    public static IntParser Default(int value) => new(value, NumberBuildDirection.MostSignificantFirst, 15);
}