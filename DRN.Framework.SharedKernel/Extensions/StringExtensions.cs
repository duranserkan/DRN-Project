using System.Globalization;
using System.Text;

namespace DRN.Framework.SharedKernel.Extensions;

public static class StringExtensions
{
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
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.DecimalDigitNumber:
                    if (NeedsUnderscore(currentCategory, previousCategory, text, currentIndex))
                    {
                        builder.Append('_');
                    }

                    currentChar = char.ToLower(currentChar, CultureInfo.InvariantCulture);
                    builder.Append(currentChar);
                    previousCategory = currentCategory;
                    break;

                default:
                    if (previousCategory != null)
                    {
                        previousCategory = UnicodeCategory.SpaceSeparator;
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    private static bool NeedsUnderscore(UnicodeCategory currentCategory, UnicodeCategory? previousCategory, string text, int currentIndex)
    {
        if (previousCategory == UnicodeCategory.SpaceSeparator)
            return true;

        if (currentCategory is UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter)
        {
            return previousCategory == UnicodeCategory.LowercaseLetter ||
                   previousCategory != UnicodeCategory.DecimalDigitNumber &&
                   previousCategory != null &&
                   currentIndex + 1 < text.Length &&
                   char.IsLower(text[currentIndex + 1]);
        }

        return false;
    }

    public static string ToCamelCase(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var words = GetWords(text);
        var camelCaseStringBuilder = new StringBuilder(text.Length);
        var isFirstWord = true;

        foreach (var word in words)
        {
            if (isFirstWord)
            {
                camelCaseStringBuilder.Append(word.ToLowerInvariant());
                isFirstWord = false;
                continue;
            }

            camelCaseStringBuilder.Append(char.ToUpperInvariant(word[0]));
            camelCaseStringBuilder.Append(word[1..].ToLowerInvariant());
        }

        return camelCaseStringBuilder.ToString();
    }

    public static string ToPascalCase(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var words = GetWords(text);
        var pascalCaseBuilder = new StringBuilder(text.Length);
        foreach (var word in words)
        {
            pascalCaseBuilder.Append(char.ToUpperInvariant(word[0]));
            pascalCaseBuilder.Append(word[1..].ToLowerInvariant());
        }

        return pascalCaseBuilder.ToString();
    }

    private static string[] GetWords(string text)
    {
        var cleanedInput = RemoveNonAlphanumeric(text);
        return cleanedInput.Split([' '], StringSplitOptions.RemoveEmptyEntries);
    }

    private static string RemoveNonAlphanumeric(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder(input.Length);
        foreach (var c in input)
            result.Append(char.IsLetterOrDigit(c) ? c : ' ');

        return result.ToString();
    }
}
