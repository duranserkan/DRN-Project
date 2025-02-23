using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DRN.Framework.Utils.Extensions;

public static class StringExtensions
{
    private const string HexChars = "0123456789abcdef";
    public static T Parse<T>(this string s, IFormatProvider? provider = null) where T : IParsable<T>
        => T.Parse(s, provider);

    public static bool TryParse<T>(this string s, out T? result, IFormatProvider? provider = null) where T : IParsable<T>
        => T.TryParse(s, provider, out result);

    public static Stream ToStream(this string value, Encoding? encoding = null) => new MemoryStream(value.ToByteArray(encoding));
    public static byte[] ToByteArray(this string value, Encoding? encoding = null) => (encoding ?? Encoding.UTF8).GetBytes(value);

    public static string GetSha512Hash(this string value)
    {
        var hashBytes = SHA512.HashData(value.ToByteArray());

        return string.Create(hashBytes.Length * 2, hashBytes, (chars, bytes) => 
        {
            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                chars[i * 2] = HexChars[b >> 4];    // the high-order 4 bits(nibbles) (first hex digit)
                chars[i * 2 + 1] = HexChars[b & 0x0F]; // the low-order 4 bits(nibbles) (second hex digit):
            }
        });
    }

    public static long GenerateLongSeedFromHash(this string input)
    {
        var hashBytes = SHA512.HashData(input.ToByteArray());
        return BitConverter.ToInt64(hashBytes, 19);
    }

    public static int GenerateIntSeedFromHash(this string input)
    {
        var hashBytes = SHA256.HashData(input.ToByteArray());
        return BitConverter.ToInt32(hashBytes, 19);
    }

    public static string ToSnakeCase(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var builder = new StringBuilder(text.Length + Math.Min(2, text.Length / 5));
        var previousCategory = default(UnicodeCategory?);

        for (var currentIndex = 0; currentIndex < text.Length; currentIndex++)
        {
            var currentChar = text[currentIndex];
            if (currentChar == '_')
            {
                builder.Append('_');
                previousCategory = null;
                continue;
            }

            var currentCategory = char.GetUnicodeCategory(currentChar);
            switch (currentCategory)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                    if (previousCategory == UnicodeCategory.SpaceSeparator ||
                        previousCategory == UnicodeCategory.LowercaseLetter ||
                        previousCategory != UnicodeCategory.DecimalDigitNumber &&
                        previousCategory != null &&
                        currentIndex > 0 &&
                        currentIndex + 1 < text.Length &&
                        char.IsLower(text[currentIndex + 1]))
                    {
                        builder.Append('_');
                    }

                    currentChar = char.ToLower(currentChar, CultureInfo.InvariantCulture);
                    break;

                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.DecimalDigitNumber:
                    if (previousCategory == UnicodeCategory.SpaceSeparator)
                    {
                        builder.Append('_');
                    }

                    break;

                default:
                    if (previousCategory != null)
                    {
                        previousCategory = UnicodeCategory.SpaceSeparator;
                    }

                    continue;
            }

            builder.Append(currentChar);
            previousCategory = currentCategory;
        }

        return builder.ToString();
    }

    public static string ToCamelCase(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var words = GetWords(text);
        var camelCaseStringBuilder = new StringBuilder(text.Length);
        var isFirstWord = true;

        foreach (string word in words)
        {
            if (isFirstWord)
            {
                camelCaseStringBuilder.Append(word.ToLower());
                isFirstWord = false;
                continue;
            }

            camelCaseStringBuilder.Append(char.ToUpper(word[0]));
            camelCaseStringBuilder.Append(word.Substring(1).ToLower());
        }

        return camelCaseStringBuilder.ToString();
    }

    public static string ToPascalCase(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var words = GetWords(text);
        var pascalCaseBuilder = new StringBuilder(text.Length);
        foreach (string word in words)
        {
            pascalCaseBuilder.Append(char.ToUpper(word[0]));
            pascalCaseBuilder.Append(word.Substring(1).ToLower());
        }

        return pascalCaseBuilder.ToString();
    }

    private static string[] GetWords(string text)
    {
        var cleanedInput = RemoveNonAlphanumeric(text);
        var words = cleanedInput.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        return words;
    }

    private static string RemoveNonAlphanumeric(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder(input.Length);

        foreach (char c in input)
            if (char.IsLetterOrDigit(c))
                result.Append(c);
            else
                result.Append(' ');

        return result.ToString();
    }
}