namespace DRN.Framework.Utils.Numbers;

public static class BitmaskExtensions
{
    public static long GetBitMaskSigned(this byte bitLength) => (1L << bitLength) - 1; // 1L << 4 => ...10000 --- (1L << 4 -1) => 01111
    public static ulong GetBitMaskUnsigned(this byte bitLength) => (1UL << bitLength) - 1; // 1UsL << 4 => ...10000 --- (1L << 4 -1) => 01111
}