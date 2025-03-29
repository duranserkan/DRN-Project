namespace DRN.Framework.Utils.Common.Numbers;

public enum NumberBuildDirection
{
    /// <summary>
    /// MSB-first: Start from top down
    /// </summary>
    MostSignificantFirst,
    /// <summary>
    /// LSB-first: Start from 0 to up
    /// </summary>
    LeastSignificantFirst
}

public enum ResidueType
{
    Nibble = 1,
    Byte,
    UShort,
    UInt
}

public static class NumberExtensions
{
    public static int GetResidueBitLength(this ResidueType type) => type switch
    {
        ResidueType.Nibble => 3,
        ResidueType.Byte => 7,
        ResidueType.UShort => 15,
        ResidueType.UInt => 31,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
    
    public static int GetResidueNibbleCount(this ResidueType type) => type switch
    {
        ResidueType.Nibble => 1,
        ResidueType.Byte => 2,
        ResidueType.UShort => 4,
        ResidueType.UInt => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}