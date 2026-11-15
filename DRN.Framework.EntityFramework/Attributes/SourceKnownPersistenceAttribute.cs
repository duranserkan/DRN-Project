namespace DRN.Framework.EntityFramework.Attributes;

/// <summary>Declares known entity membership for convention analysis of a context.</summary>
/// <remarks>
/// This metadata does not add or remove an entity from the EF model. Configure the model separately.
/// Cross-subdomain use still requires its own narrow, reasoned diagnostic suppression.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SourceKnownPersistenceAttribute(Type entity) : Attribute
{
    public Type Entity { get; } = entity;
    public SourceKnownPersistenceInclusion Inclusion { get; set; } = SourceKnownPersistenceInclusion.Include;
    public string? Reason { get; set; }
}

/// <summary>The declared convention-analysis membership, independent of EF model configuration.</summary>
public enum SourceKnownPersistenceInclusion
{
    Include,
    Exclude
}
