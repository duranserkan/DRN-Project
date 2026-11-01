namespace DRN.Framework.SharedKernel.Attributes;

/// <summary>Build-generated classification catalog retained in implementation and reference assemblies.</summary>
/// <remarks>Register a data-classification-rules.json build item instead of writing this attribute manually.</remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class DataClassificationRulesAttribute(string rules) : Attribute
{
    public string Rules { get; } = rules;
}
