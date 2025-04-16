namespace DRN.Framework.Utils.Numbers;

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
    Bit = 1,
    Crumb = 2,
    Nibble = 4,
    Byte = 8,
    UShort = 16,
    UInt = 32
}

public static class NumberExtensions
{
    public static byte GetResidueBitLength(this ResidueType type) => type switch
    {
        ResidueType.UInt => 31,
        ResidueType.UShort => 15,
        ResidueType.Byte => 7,
        ResidueType.Nibble => 3,
        ResidueType.Crumb => 1,
        ResidueType.Bit => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static long GetBitMask(this byte bitLength) => (1L << bitLength) - 1; // 1L << 4 => ...10000 --- (1L << 4 -1) => 01111
}