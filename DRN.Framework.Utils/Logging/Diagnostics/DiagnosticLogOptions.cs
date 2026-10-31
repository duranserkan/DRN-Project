using System.ComponentModel.DataAnnotations;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Logging.Diagnostics;

[Config("DiagnosticLogging")]
public sealed record DiagnosticLogOptions : IValidatableObject
{
    [Range(1024, 262144)] public int MaxFieldBytes { get; init; } = 65536;
    [Range(1024, 1048576)] public int MaxInputBytes { get; init; } = 262144;
    [Range(4096, 1048576)] public int MaxScopeBytes { get; init; } = 131072;
    [Range(4096, 65536)] public int MaxRecordBytes { get; init; } = 32768;
    [Range(1, 16)] public int MaxRecords { get; init; } = 8;
    [Range(1, 16)] public int MaxDepth { get; init; } = 8;
    [Range(16, 4096)] public int MaxNodes { get; init; } = 1024;
    [Range(1, 1024)] public int MaxCollectionEntries { get; init; } = 256;
    [Sensitive(false, Justification = "Diagnostic key size budget."), Range(32, 512)] public int MaxKeyBytes { get; init; } = 256;
    [Range(1, 100)] public int MaxProcessingMilliseconds { get; init; } = 50;
    public string[] SensitiveFields { get; init; } = [];
    public string[] SensitiveFieldPaths { get; init; } = [];
    public string[] SensitivePathSegments { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SensitiveFields == null || SensitiveFieldPaths == null || SensitivePathSegments == null)
        {
            yield return new ValidationResult("Diagnostic rule collections cannot be null.");
            yield break;
        }
        if (MaxFieldBytes > MaxInputBytes)
            yield return new ValidationResult("Individual diagnostic limits must not exceed their shared budget.");
        if (SensitiveFields.Length + SensitiveFieldPaths.Length + SensitivePathSegments.Length > 256 ||
            SensitiveFields.Concat(SensitiveFieldPaths).Concat(SensitivePathSegments).Any(value => string.IsNullOrWhiteSpace(value) || value.Length > MaxKeyBytes))
            yield return new ValidationResult("Additional diagnostic rules must contain at most 256 bounded, nonempty names or paths.");
    }
}
