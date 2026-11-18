using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

internal static class IntegralConstant
{
    private static (int Bits, bool Signed) Storage(SpecialType type) => type switch
    {
        SpecialType.System_SByte => (8, true),
        SpecialType.System_Byte => (8, false),
        SpecialType.System_Int16 => (16, true),
        SpecialType.System_UInt16 or SpecialType.System_Char => (16, false),
        SpecialType.System_Int32 => (32, true),
        SpecialType.System_UInt32 => (32, false),
        SpecialType.System_Int64 => (64, true),
        SpecialType.System_UInt64 => (64, false),
        _ => default
    };

    internal static bool Supports(ITypeSymbol? type) => type is not null && Storage(type.SpecialType).Bits != 0;

    internal static object? ConvertValue(object value, SpecialType target, bool isChecked)
    {
        var (bits, signed) = Storage(target);
        if (bits == 0) return null;
        var modulus = bits == 64 ? (decimal)ulong.MaxValue + 1 : 1UL << bits;
        var limit = signed ? modulus / 2 : modulus;
        var min = signed ? -limit : 0;
        decimal number;
        if (value is float or double)
        {
            var truncated = Math.Truncate(Convert.ToDouble(value));
            if (!(truncated >= (double)min && truncated < (double)limit)) return null;
            number = truncated < 0 ? (decimal)(long)truncated : (ulong)truncated;
        }
        else if (value is char character) number = character;
        else if (value is sbyte or byte or short or ushort or int or uint or long or ulong or decimal)
            number = decimal.Truncate(Convert.ToDecimal(value));
        else return null;

        if (number < min || number >= limit)
        {
            if (isChecked || value is decimal) return null;
            number %= modulus;
            if (number < min) number += modulus;
            if (number >= limit) number -= modulus;
        }
        return target switch
        {
            SpecialType.System_Byte => (object)(byte)number,
            SpecialType.System_UInt64 => (ulong)number,
            _ => (long)number
        };
    }
}
