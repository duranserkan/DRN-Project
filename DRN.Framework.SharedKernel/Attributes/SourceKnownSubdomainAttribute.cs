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

    /// <summary>Gets the declared subdomain or technical area name.</summary>
    public string Name { get; }

    /// <summary>Gets or sets the optional owning domain name when overriding or disambiguating multi-domain ownership.</summary>
    public string? Domain { get; set; }

    /// <summary>Gets the external type targeted by an assembly-level ownership declaration, or <see langword="null"/> on a type-level declaration.</summary>
    public Type? TargetType { get; }

    /// <summary>Gets or sets the required rationale for technical aliases, exceptional placement, or externally owned types.</summary>
    public string? Reason { get; set; }
}
