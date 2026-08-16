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
        description: "All non-abstract classes that derive from SourceKnownEntity must declare an [EntityType<TApp>] or derived domain attribute with a unique byte identifier per AppId.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor DuplicateEntityTypeValue = new(
        id: "DRN0002",
        title: "Duplicate EntityType value",
        messageFormat: "EntityType value '{0}' with AppId '{1}' on '{2}' is already used by '{3}'. EntityType values must be unique per AppId across the domain dependency graph, including referenced assemblies.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every domain entity inheriting from SourceKnownEntity must declare a unique EntityType byte value per AppId across the domain dependency graph.",
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
        messageFormat: "Entity class name '{0}' is already used by '{1}' within AppId '{2}'. Duplicate entity names within the same AppId can cause EF Core mapping or messaging ambiguity.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Domain entities inheriting from SourceKnownEntity should have unique class names per AppId across the domain model to avoid mapping and messaging ambiguity.",
        helpLinkUri: HelpLinkUri,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);
}
