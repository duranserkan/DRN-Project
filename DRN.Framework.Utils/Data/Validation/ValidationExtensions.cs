using System.ComponentModel.DataAnnotations;
using ValidationException = DRN.Framework.SharedKernel.ValidationException;

namespace DRN.Framework.Utils.Data.Validation;

public static class ValidationExtensions
{
    public static Validation ValidateDataAnnotations(this object obj)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(obj);
        var isValid = Validator.TryValidateObject(obj, validationContext, validationResults, true);

        return new Validation
        {
            IsValid = isValid,
            Errors = validationResults
        };
    }

    public static void ValidateDataAnnotationsThrowIfInvalid(this object obj, string? messagePrefix = null)
    {
        var result = obj.ValidateDataAnnotations();
        if (result.IsValid) return;

        messagePrefix = string.IsNullOrWhiteSpace(messagePrefix) ? string.Empty : $"{messagePrefix}{Environment.NewLine}";
        var errorMessages = string.Join(Environment.NewLine, result.Errors.Select(e => e.ErrorMessage));
        throw new ValidationException($"""
                                       {messagePrefix}Validation failed:
                                       {errorMessages}
                                       """);
    }
}