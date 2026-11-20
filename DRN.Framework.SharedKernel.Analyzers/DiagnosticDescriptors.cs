namespace DRN.Framework.SharedKernel.Analyzers;

public static class DiagnosticDescriptors
{
    private const string Category = "DRN.Domain";
    [SuppressMessage("SonarQube", "S1075", Justification = "Canonical public documentation URL for Roslyn diagnostic help links, not an environment-specific resource endpoint.")]
    public const string HelpLinkUri = "https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers";

    public static readonly DiagnosticDescriptor SourceKnownControllerPolicyRequired = new(
        "DRNAUTHORIZATION001", "Declare a named policy for Source-Known controller actions",
        "Source-Known controller '{0}' action '{1}' requires an explicit named authorization policy: {2}",
        "DRN.Security", DiagnosticSeverity.Error, true, helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor MissingEntityTypeAttribute = new(
        id: "DRNENTITY001",
        title: "Missing entity type attribute on SourceKnownEntity descendant",
        messageFormat: "Class '{0}' inherits from SourceKnownEntity but does not declare a derived entity type attribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Concrete, effectively non-private SourceKnownEntity classes must declare a derived domain attribute with a unique byte-backed enum value per AppId.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor DuplicateEntityTypeValue = new(
        id: "DRNENTITY002",
        title: "Duplicate EntityType value",
        messageFormat: "EntityType value '{0}' with AppId '{1}' on '{2}' is already used by '{3}'. EntityType values must be unique per AppId across the domain dependency graph, including referenced assemblies.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every domain entity inheriting from SourceKnownEntity must declare a unique EntityType byte value per AppId across the domain dependency graph.",
        helpLinkUri: HelpLinkUri,
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor InvalidEntityTypeAttributeUsage = new(
        id: "DRNENTITY003",
        title: "Invalid entity type attribute usage",
        messageFormat: "Class '{0}' is decorated with an entity type attribute but is abstract, effectively private, or does not inherit from SourceKnownEntity",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Derived entity type attributes should only be applied to non-abstract classes that inherit from SourceKnownEntity and are neither private nor nested within a private type.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor DuplicateEntityName = new(
        id: "DRNENTITY004",
        title: "Duplicate entity class name",
        messageFormat: "Entity class name '{0}' is already used by '{1}' within AppId '{2}'. Duplicate entity names within the same AppId can cause EF Core mapping or messaging ambiguity.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Domain entities inheriting from SourceKnownEntity should have unique class names per AppId across the domain model to avoid mapping and messaging ambiguity.",
        helpLinkUri: HelpLinkUri,
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor MultipleAppIdsNotPermitted = new(
        id: "DRNENTITY005",
        title: "Multiple AppIds in single application compilation",
        messageFormat: "Project '{0}' contains entity types declaring multiple distinct AppIds ({1}). Production projects must only declare a single AppId unless <AllowMultipleAppIds>true</AllowMultipleAppIds> is set in the project file.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Domain entity types within a single application project graph must belong to the same AppId partition to prevent ID collisions and cross-partition entity substitution.",
        helpLinkUri: HelpLinkUri,
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor UnresolvableAppId = new(
        id: "DRNENTITY006",
        title: "Unresolvable or non-constant application partition",
        messageFormat: "AppId for '{0}' could not be statically determined. Publish a public constant named Value or AppId and return its value from the getter implementing IAppId.AppId.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Application partition identifiers (IAppId) must declare a constant value to allow static compile-time validation across assembly boundaries.",
        helpLinkUri: HelpLinkUri,
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor AppIdOutOfRange = new(
        id: "DRNENTITY007",
        title: "AppId is outside the supported range",
        messageFormat: "AppId value '{1}' for '{0}' must be between 0 and 127",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Application partition identifiers (IAppId) must be between 0 and 127 so they fit the Source-Known ID partition field.",
        helpLinkUri: HelpLinkUri,
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor UnsupportedEntityAttribute = new(
        id: "DRNENTITY008",
        title: "Unsupported entity attribute constructor",
        messageFormat: "Unsupported entity attribute '{0}'{1}. Derive from EntityTypeAttribute<TApp> and declare one constructor that forwards one byte-backed enum parameter unchanged to its base constructor.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Derived entity attributes must preserve their constructor argument as the entity type. Compiled references must satisfy the same forwarding contract.",
        helpLinkUri: HelpLinkUri,
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor InvalidAppIdArgument = new(
        "DRNENTITY009", "Invalid constant application partition",
        "AppId value '{0}' must be between 0 and 127", Category,
        DiagnosticSeverity.Error, true, helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor InvalidEntityIdFormatArgument = new(
        "DRNENTITY010", "Undefined constant entity ID format",
        "Entity ID format value '{0}' is not a defined SourceKnownEntityIdFormat", Category,
        DiagnosticSeverity.Error, true, helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor HiddenEntityIdentityMember = new(
        "DRNENTITY011", "Hidden entity identity member",
        "Member '{0}' hides SourceKnownEntity.{1}; framework and consumer code can observe different identities", Category,
        DiagnosticSeverity.Warning, true, helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GenericConcreteEntity = new(
        "DRNENTITY012", "Concrete generic entity shares identity across constructed types",
        "Entity '{0}' has generic type parameters of its own or a containing type; use an abstract generic base and separately attributed concrete descendants", Category,
        DiagnosticSeverity.Warning, true, helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor AbstractEntityMetadataArgument = new(
        "DRNENTITY013", "Use a concrete entity type",
        "Abstract entity '{0}' cannot be used with '{1}'; a concrete entity type is required", Category,
        DiagnosticSeverity.Error, true, helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor UninitializedEntityOperations = new(
        "DRNENTITY014", "Entity ID operations are not initialized",
        "'{0}' is called on an entity whose ID operations are not initialized; use the injected ID utility before entity initialization", Category,
        DiagnosticSeverity.Error, true, helpLinkUri: HelpLinkUri);
}
