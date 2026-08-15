using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

public static class DiagnosticDescriptors
{
    private const string Category = "DRN.Domain";

    public static readonly DiagnosticDescriptor MissingEntityTypeAttribute = new(
        id: "DRN0001",
        title: "Missing [EntityType] attribute on SourceKnownEntity descendant",
        messageFormat: "Class '{0}' inherits from SourceKnownEntity but does not have the [EntityType] attribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All non-abstract classes that derive from SourceKnownEntity must declare an [EntityType] attribute with a unique byte identifier.");

    public static readonly DiagnosticDescriptor DuplicateEntityTypeValue = new(
        id: "DRN0002",
        title: "Duplicate EntityType value",
        messageFormat: "EntityType value '{0}' on '{1}' is already used by '{2}'. EntityType values must be unique across the domain.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every domain entity inheriting from SourceKnownEntity must declare a unique EntityType byte value.");

    public static readonly DiagnosticDescriptor InvalidEntityTypeAttributeUsage = new(
        id: "DRN0003",
        title: "Invalid [EntityType] attribute usage",
        messageFormat: "Class '{0}' is decorated with [EntityType] attribute but is abstract or does not inherit from SourceKnownEntity",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [EntityType] attribute should only be applied to non-abstract classes that inherit from SourceKnownEntity.");
}
