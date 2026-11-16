namespace DRN.Framework.SharedKernel.Attributes;

/// <summary>Declares the business subdomain or technical area that owns a type.</summary>
/// <remarks>
/// Use a reason for aliases, exceptional placement and externally owned types.
/// Ownership does not configure persistence or suppress cross-subdomain use diagnostics.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
    AllowMultiple = true, Inherited = false)]
public sealed class SourceKnownSubdomainAttribute : Attribute
{
    public SourceKnownSubdomainAttribute(string name) => Name = name;

    /// <summary>Declares ownership of an exact external type at assembly scope.</summary>
    public SourceKnownSubdomainAttribute(Type targetType, string name)
    {
        TargetType = targetType;
        Name = name;
    }

    public string Name { get; }
    public Type? TargetType { get; }
    public string? Reason { get; set; }
}
