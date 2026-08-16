using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

public static class DiagnosticDescriptors
{
    private const string Category = "DRN.Domain";
    public const string HelpLinkUri = "https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers";

    public static readonly DiagnosticDescriptor MissingEntityTypeAttribute = new(
        id: "DRN0001",
        title: "Missing [EntityType] attribute on SourceKnownEntity descendant",
        messageFormat: "Class '{0}' inherits from SourceKnownEntity but does not have the [EntityType] attribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All non-abstract classes that derive from SourceKnownEntity must declare an [EntityType] attribute with a unique byte identifier.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor DuplicateEntityTypeValue = new(
        id: "DRN0002",
        title: "Duplicate EntityType value",
        messageFormat: "EntityType value '{0}' on '{1}' is already used by '{2}'. EntityType values must be unique across the domain dependency graph, including referenced assemblies.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every domain entity inheriting from SourceKnownEntity must declare a unique EntityType byte value across the domain dependency graph.",
        helpLinkUri: HelpLinkUri,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor InvalidEntityTypeAttributeUsage = new(
        id: "DRN0003",
        title: "Invalid [EntityType] attribute usage",
        messageFormat: "Class '{0}' is decorated with [EntityType] attribute but is abstract, private, or does not inherit from SourceKnownEntity",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [EntityType] attribute should only be applied to non-abstract, non-private classes that inherit from SourceKnownEntity.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor DuplicateEntityName = new(
        id: "DRN0004",
        title: "Duplicate entity class name",
        messageFormat: "Entity class name '{0}' is already used by '{1}'. Duplicate entity names across the domain model can cause EF Core mapping or messaging ambiguity.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Domain entities inheriting from SourceKnownEntity should have unique class names across the domain model to avoid mapping and messaging ambiguity.",
        helpLinkUri: HelpLinkUri,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);
}
