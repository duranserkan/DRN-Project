using System.ComponentModel.DataAnnotations;

namespace DRN.Framework.Utils.Data.Validation;

public class Validation
{
    public bool IsValid { get; set; }
    public List<ValidationResult> Errors { get; set; } = [];
}