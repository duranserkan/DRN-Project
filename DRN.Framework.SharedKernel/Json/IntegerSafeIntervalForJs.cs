namespace DRN.Framework.SharedKernel.Json;

public static class IntegerSafeIntervalForJs
{
    /// <summary>
    /// The maximum integer value that JavaScript can safely represent without loss of precision:
    /// 2⁵³ − 1.
    /// </summary>
    public const long Max = 9_007_199_254_740_991L;

    /// <summary>
    /// The minimum integer value that JavaScript can safely represent without loss of precision:
    /// −(2⁵³ − 1).
    /// </summary>
    public const long Min = -9_007_199_254_740_991L;
}