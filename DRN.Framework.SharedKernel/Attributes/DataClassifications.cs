using Microsoft.Extensions.Compliance.Classification;

namespace DRN.Framework.SharedKernel.Attributes;

/// <summary>Classifications for data that requires diagnostic redaction.</summary>
public static class DataClassifications
{
    public const string TaxonomyName = "DRN.Classification";
    public static DataClassification Sensitive { get; } = new(TaxonomyName, nameof(Sensitive));
    public static DataClassification Personal { get; } = new(TaxonomyName, nameof(Personal));
    public static DataClassificationSet Set { get; } = new(Sensitive, Personal);
}

/// <summary>Replaces a diagnostic value without reading its getter. Does not affect business serialization.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
public class SensitiveAttribute : DataClassificationAttribute
{
    public SensitiveAttribute() : this(true) { }

    /// <summary>False explicitly exempts this property from built-in diagnostic name rules.</summary>
    public SensitiveAttribute(bool sensitive) : base(sensitive ? DataClassifications.Sensitive : DataClassification.None)
    {
        if (!sensitive && GetType() != typeof(SensitiveAttribute))
            throw new ArgumentException("Derived sensitive attributes must declare positive classification.", nameof(sensitive));
        IsSensitive = sensitive;
    }

    public bool IsSensitive { get; }
    public string? Justification { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
public class PersonalAttribute() : DataClassificationAttribute(DataClassifications.Personal);
