using System.Collections.Frozen;
using System.ComponentModel.DataAnnotations;

namespace DRN.Framework.SharedKernel.Attributes;

/// <summary>
/// Validates that a string meets secure key requirements:
/// - Length within [MinLength, MaxLength]
/// - Contains required character classes (uppercase, lowercase, digit, special)
/// - Only allows safe, non-delimiter characters: alphanumeric + ! * ( ) - _
/// - Optionally restricts sequential and repeated characters
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SecureKeyAttribute() : ValidationAttribute(DefaultErrorMessage)
{
    public static readonly FrozenSet<char> SpecialChars = " !*()-_".ToFrozenSet();
    public static readonly FrozenSet<char> AllowedChars = "ABCÇDEFGHIİJKLMNOÖPQRSŞTUÜVWXYZabcçdefghıijklmnoöpqrsştuüvwxyz0123456789 !*()-_".ToFrozenSet();

    private const string DefaultErrorMessage =
        "Key must be between {0} and {1} characters long and contain uppercase, lowercase, digit, space or and at least one special character from: ! * ( ) - _";

    public ushort MinLength { get; set; } = 16;
    public ushort MaxLength { get; set; } = 256;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialChar { get; set; } = true;

    /// <summary>
    /// Maximum length of sequential character runs allowed (case-insensitive for letters, digits only).
    /// Example: MaxSequentialRunLength = 3 means "abcd" or "4567" is invalid (4 sequential), but "abc" is valid.
    /// </summary>
    public byte MaxSequentialChars { get; set; } = 4;

    /// <summary>
    /// Maximum number of identical consecutive characters allowed.
    /// Example: MaxConsecutiveRepetitions = 2 means "aaa" is invalid (3 repeats), but "aa" is valid.
    /// </summary>
    public byte MaxRepeatedChars { get; set; } = 3;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        string[] memberName = string.IsNullOrEmpty(validationContext.MemberName) ? [] : [validationContext.MemberName];

        if (value is not string key)
            return value == null
                ? new ValidationResult("Key is required.", memberName)
                : new ValidationResult("SecureKeyAttribute can only be applied to string properties, fields, or parameters.", memberName);

        if (MinLength < 1 || MaxLength < 1 || MinLength > MaxLength)
            return new ValidationResult($"SecureKeyAttribute is misconfigured: MinLength={MinLength}, MaxLength={MaxLength}. Both must be ≥1 and MinLength ≤ MaxLength.",
                memberName);
        if (key.Length < MinLength || key.Length > MaxLength)
            return new ValidationResult($"Key must be between {MinLength} and {MaxLength} characters long.", memberName);

        var classification = SecureKeyClassificationResult.Classify(key);
        var result = ValidateClassification(classification, memberName);
        if (result is not null)
            return result;

        // Sequential character check (e.g., 'abcd', '4567')
        if (MaxSequentialChars >= 2 && HasSequentialCharacters(key, MaxSequentialChars))
            return new ValidationResult($"Key cannot contain more than {MaxSequentialChars} sequential characters in a row.", memberName);

        // Repeated character check (e.g., 'aaa')
        if (MaxRepeatedChars >= 2 && HasTooManyRepeatedCharacters(key, MaxRepeatedChars))
            return new ValidationResult($"Key cannot contain more than {MaxRepeatedChars} identical characters in a row.", memberName);

        return ValidationResult.Success;
    }

    private ValidationResult? ValidateClassification(SecureKeyClassificationResult classificationResult, IEnumerable<string> memberName)
    {
        if (classificationResult.HasNonAllowed)
            return new ValidationResult($"Key contains invalid character '{classificationResult.NonAllowed}'. Allowed characters are alphanumeric and: ! * ( ) - _", memberName);
        if (RequireUppercase && !classificationResult.HasUpper)
            return new ValidationResult("Key must contain at least one uppercase letter.", memberName);
        if (RequireLowercase && !classificationResult.HasLower)
            return new ValidationResult("Key must contain at least one lowercase letter.", memberName);
        if (RequireDigit && !classificationResult.HasDigit)
            return new ValidationResult("Key must contain at least one digit.", memberName);
        if (RequireSpecialChar && !classificationResult.HasSpecial)
            return new ValidationResult("Key must contain at least one special character: !, *, (, ), -, or _.", memberName);

        return null;
    }


    /// <summary>
    /// Checks for ascending or descending sequences of characters (case-insensitive, ASCII-only).
    /// Only considers sequences within the same character class (letters or digits) implicitly,
    /// but relies on input being restricted to AllowedChars.
    /// </summary>
    private static bool HasSequentialCharacters(string key, byte maxLength)
    {
        if (maxLength < 2 || key.Length < maxLength) return false;

        var prev = key[0];
        var ascCount = 1;
        var descCount = 1;

        for (var i = 1; i < key.Length; i++)
        {
            var curr = key[i];

            // Check ascending sequence
            if (curr == prev + 1)
            {
                ascCount++;
                if (ascCount > maxLength) return true;
            }
            else
            {
                ascCount = 1;
            }

            // Check descending sequence
            if (curr == prev - 1)
            {
                descCount++;
                if (descCount > maxLength) return true;
            }
            else
            {
                descCount = 1;
            }

            prev = curr;
        }

        return false;
    }

    /// <summary>
    /// Checks for repeated identical characters beyond the allowed limit.
    /// </summary>
    private static bool HasTooManyRepeatedCharacters(string key, byte maxAllowed)
    {
        if (maxAllowed < 2) return false;

        var count = 1;
        for (var i = 1; i < key.Length; i++)
        {
            if (key[i] == key[i - 1])
            {
                count++;
                if (count > maxAllowed)
                    return true;
            }
            else
            {
                count = 1;
            }
        }

        return false;
    }

    public override string FormatErrorMessage(string name) =>
        string.Format(ErrorMessageString, MinLength, MaxLength);
}

public struct SecureKeyClassificationResult
{
    public bool HasUpper { get; set; }
    public bool HasLower { get; set; }
    public bool HasDigit { get; set; }
    public bool HasSpecial { get; set; }
    public bool HasNonAllowed { get; set; }
    public char NonAllowed { get; set; }

    public static SecureKeyClassificationResult Classify(string key)
    {
        var nonAllowed = '\0';
        bool hasUpper = false, hasLower = false, hasDigit = false, hasSpecial = false, hasNonAllowed = false;
        foreach (var c in key)
        {
            if (!SecureKeyAttribute.AllowedChars.Contains(c))
            {
                hasNonAllowed = true;
                nonAllowed = c;
                break;
            }

            if (c is >= 'A' and <= 'Z') hasUpper = true;
            else if (c is >= 'a' and <= 'z') hasLower = true;
            else if (c is >= '0' and <= '9') hasDigit = true;
            else if (SecureKeyAttribute.SpecialChars.Contains(c)) hasSpecial = true;
        }

        return new SecureKeyClassificationResult
        {
            HasDigit = hasDigit, HasLower = hasLower, HasUpper = hasUpper,
            HasSpecial = hasSpecial, HasNonAllowed = hasNonAllowed, NonAllowed = nonAllowed
        };
    }
}