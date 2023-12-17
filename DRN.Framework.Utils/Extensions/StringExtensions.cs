using System.Text;

namespace DRN.Framework.Utils.Extensions;

public static class StringExtensions
{
    public static Stream ToStream(this string value, Encoding? encoding = null) => new MemoryStream((encoding ?? Encoding.UTF8).GetBytes(value));
}