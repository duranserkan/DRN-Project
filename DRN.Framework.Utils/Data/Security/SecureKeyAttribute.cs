using System.ComponentModel.DataAnnotations;

namespace DRN.Framework.Utils.Data.Security;
//todo: write tests and add special character rule
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class SecureKeyAttribute() : ValidationAttribute(DefaultErrorMessage)
{
    private const string DefaultErrorMessage =
        "Key must be at least {0} characters long and contain uppercase, lowercase, and digit";

    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 128;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public int MaxSequentialChars { get; set; } = 5;

    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        // Only allow string types
        if (value != null && value.GetType() != typeof(string))
            return new ValidationResult("SecureKeyAttribute can only be applied to string properties, fields, or parameters.");

        if (string.IsNullOrWhiteSpace(value?.ToString()))
            return new ValidationResult("Key is required.");

        var key = value.ToString()!;
        
        if (key.Length < MinLength || key.Length > MaxLength)
            return new ValidationResult($"Key must be between {MinLength} and {MaxLength} characters long.");

        // Security checks
        if (key.Contains('\0'))
            return new ValidationResult("Key cannot contain null characters.");

        if (key.Any(char.IsWhiteSpace))
            return new ValidationResult("Key cannot contain whitespace.");

        // Single pass for all character checks
        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;

        foreach (var c in key)
        {
            if (char.IsUpper(c)) hasUpper = true;
            if (char.IsLower(c)) hasLower = true;
            if (char.IsDigit(c)) hasDigit = true;

            // Early exit if all requirements met
            if (hasUpper && hasLower && hasDigit)
                break;
        }

        // Safe sequential character check
        if (MaxSequentialChars > 0 && HasSequentialCharacters(key, MaxSequentialChars))
            return new ValidationResult($"Key cannot contain {MaxSequentialChars} or more sequential characters (e.g., '12345', 'abcde').");

        // Safe repeated character check
        if (HasTooManyRepeatedCharacters(key))
            return new ValidationResult("Key cannot contain 5 or more repeated characters in a row.");

        // Complexity requirements
        if (RequireUppercase && !hasUpper)
            return new ValidationResult("Key must contain at least one uppercase letter.");

        if (RequireLowercase && !hasLower)
            return new ValidationResult("Key must contain at least one lowercase letter.");

        if (RequireDigit && !hasDigit)
            return new ValidationResult("Key must contain at least one digit.");

        return ValidationResult.Success!;
    }

    private static bool HasSequentialCharacters(string key, int maxLength)
    {
        if (maxLength < 2 || key.Length < maxLength)
            return false;

        var ascCount = 1;
        var descCount = 1;

        for (var i = 1; i < key.Length; i++)
        {
            var curr = char.ToLower(key[i]);
            var prev = char.ToLower(key[i - 1]);

            // Check ascending
            if (curr == prev + 1)
            {
                ascCount++;
                if (ascCount >= maxLength)
                    return true;
            }
            else
            {
                ascCount = 1;
            }

            // Check descending
            if (curr == prev - 1)
            {
                descCount++;
                if (descCount >= maxLength)
                    return true;
            }
            else
            {
                descCount = 1;
            }
        }

        return false;
    }

    private static bool HasTooManyRepeatedCharacters(string key)
    {
        var count = 1;
        for (var i = 1; i < key.Length; i++)
        {
            if (key[i] == key[i - 1])
            {
                count++;
                if (count >= 5)
                    return true;
            }
            else
            {
                count = 1;
            }
        }

        return false;
    }

    public override string FormatErrorMessage(string name) => string.Format(ErrorMessageString, MinLength);
}