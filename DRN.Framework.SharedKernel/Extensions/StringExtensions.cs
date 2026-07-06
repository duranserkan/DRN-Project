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
