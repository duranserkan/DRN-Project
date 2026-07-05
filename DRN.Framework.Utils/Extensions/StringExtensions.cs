using System.Globalization;
using System.Text;

namespace DRN.Framework.Utils.Extensions;

public static class StringExtensions
{
    public static T Parse<T>(this string s, IFormatProvider? provider = null) where T : IParsable<T>
        => T.Parse(s, provider);

    public static bool TryParse<T>(this string s, out T? result, IFormatProvider? provider = null) where T : IParsable<T>
        => T.TryParse(s, provider, out result);

    public static Stream ToStream(this string value, Encoding? encoding = null) => new MemoryStream(value.ToByteArray(encoding));
    public static byte[] ToByteArray(this string value, Encoding? encoding = null) => (encoding ?? Encoding.UTF8).GetBytes(value);
}