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
        WellKnownDiagnosticTags.CompilationEnd);

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
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor MultipleAppIdsNotPermitted = new(
        id: "DRN0005",
        title: "Multiple AppIds in single application compilation",
        messageFormat: "Project '{0}' contains entity types declaring multiple distinct AppIds ({1}). Production projects must only declare a single AppId unless <AllowMultipleAppIds>true</AllowMultipleAppIds> is set in the project file.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Domain entity types within a single application project graph must belong to the same AppId partition to prevent ID collisions and cross-partition entity substitution.",
        helpLinkUri: HelpLinkUri,
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor UnresolvableAppId = new(
        id: "DRN0006",
        title: "Unresolvable or non-constant AppId in [EntityType] declaration",
        messageFormat: "AppId on entity '{0}' could not be statically determined. Types implementing IAppId must declare a constant value ('public const byte Value = ...;' or 'public const byte AppId = ...;') so they can be read from metadata across assemblies.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Application partition identifiers (IAppId) must declare a constant value to allow static compile-time validation across assembly boundaries.",
        helpLinkUri: HelpLinkUri,
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor AppIdOutOfRange = new(
        id: "DRN0007",
        title: "AppId is outside the supported range",
        messageFormat: "AppId value '{1}' on entity '{0}' must be between 0 and 127",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Application partition identifiers (IAppId) must be between 0 and 127 so they fit the Source-Known ID partition field.",
        helpLinkUri: HelpLinkUri,
        WellKnownDiagnosticTags.CompilationEnd);
}
