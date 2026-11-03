using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.SharedKernel.Diagnostics;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Logging.Diagnostics;

[Config("DiagnosticLogging")]
public sealed record DiagnosticLogOptions : IValidatableObject
{
    private static readonly ConditionalWeakTable<DiagnosticLogOptions, Lazy<bool>> ValidatedRuleSources = new();

    /// <summary>Optional project rule file. Relative paths resolve from AppContext.BaseDirectory. Loaded once per options instance.</summary>
    public string? RuleFile { get; init; }
    /// <summary>Optional application assembly name for loading its catalog and those of referenced SharedKernel consumers.</summary>
    public string? RuleAssemblyName { get; init; }
    internal bool RulesValidated { get; init; }
    internal string? LoadedRuleFile { get; init; }
    internal string? LoadedRuleAssemblyName { get; init; }

    internal void ValidateRuleSources()
    {
        if (RulesValidated && RuleFile == LoadedRuleFile && RuleAssemblyName == LoadedRuleAssemblyName) return;
        if (RuleFile == null && RuleAssemblyName == null) return;
        // Preserve validation failures and once-per-options I/O without retaining unused catalog graphs.
        _ = ValidatedRuleSources.GetValue(this, static options => new Lazy<bool>(() =>
        {
            EmbeddedClassificationRules.Load(options.RuleAssemblyName).Merge(options.RuleFile == null
                ? DiagnosticRedactionRules.Empty : ReadRules(options.RuleFile));
            return true;
        })).Value;
    }

    private static DiagnosticRedactionRules ReadRules(string path)
    {
        var fullPath = Path.GetFullPath(path, AppContext.BaseDirectory);
        using var reader = new StreamReader(fullPath, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        // Allow a UTF-8 BOM and one extra character to detect overflow without reading the entire file.
        var buffer = new char[DiagnosticRedactionRules.MaxCharacters + 2];
        var count = reader.ReadBlock(buffer, 0, buffer.Length);
        var text = new string(buffer, 0, count);
        if (text.StartsWith('\uFEFF')) text = text[1..];
        return DiagnosticRedactionRules.Parse(text);
    }

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
    /// <summary>Legacy configuration, validated but not matched at runtime. Use explicit classifications.</summary>
    public string[] SensitiveFields { get; init; } = [];
    /// <summary>Legacy configuration, validated but not matched at runtime. Analyzer paths belong in the project catalog.</summary>
    public string[] SensitiveFieldPaths { get; init; } = [];
    /// <summary>Legacy configuration, validated but not matched against URL segments.</summary>
    public string[] SensitivePathSegments { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RuleFile != null && string.IsNullOrWhiteSpace(RuleFile))
            yield return new ValidationResult("A configured diagnostic redaction rule file path cannot be empty.");
        if (RuleAssemblyName != null && string.IsNullOrWhiteSpace(RuleAssemblyName))
            yield return new ValidationResult("A configured classification root assembly name cannot be empty.");
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
