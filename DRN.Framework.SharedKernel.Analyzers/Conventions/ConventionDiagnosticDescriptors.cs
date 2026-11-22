namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionDiagnosticDescriptors
{
    private const string ConfigurationArea = "Configuration";
    private const string OwnershipArea = "Ownership";
    private const string StructureArea = "Structure";
    private const string ContextArea = "Context";
    private const string PersistenceArea = "Persistence";
    private const string RepositoryArea = "Repository";
    private const string RegistrationArea = "Registration";
    private const string ApplicationArea = "Application";
    private const string ContractArea = "Contract";

    internal static readonly DiagnosticDescriptor EntityDto = Create("DRNDTO001", "Provide an entity-owned DTO projection",
        "'{0}' must override public ToDto() returning {1} from its Contract subdomain and constructing it with this.", ContractArea);

    internal static readonly DiagnosticDescriptor DtoFolder = Create("DRNDTO002", "Place the DTO in its entity folder",
        "Place '{0}' under its subdomain in the '{1}' folder and matching namespace.", StructureArea);

    internal static readonly DiagnosticDescriptor DtoClassification = Create("DRNDTO003", "Preserve projected property classification",
        "Projected property '{0}' requires [{1}] from mapped property '{2}'.", ContractArea);

    internal static readonly DiagnosticDescriptor DtoMessagePlacement = Create("DRNDTO004", "Place entity requests and responses beside their DTO",
        "Place '{0}' in '{1}' with namespace '{2}' alongside its entity DTO.", StructureArea);

    internal static readonly DiagnosticDescriptor DtoExposure = Create("DRNDTO005", "Do not expose DTOs in public contracts or endpoints",
        "Public contract or endpoint '{0}' exposes DTO type '{1}'; use an explicit Contract Request or Response without inheriting or nesting a DTO.", ContractArea);

    internal static readonly DiagnosticDescriptor ExplicitContractMessage = Create("DRNDTO006", "Use explicit Contract Request and Response types on public endpoints",
        "Public endpoint '{0}' must use an explicit Contract {1} type instead of '{2}'.", ContractArea);

    internal static readonly DiagnosticDescriptor DomainEnumRequiresMarker = Create("DRNENUM001", "Define application enums in Contract or mark Domain-only",
        "Enum '{0}' is declared in Domain without [DomainOnly]. Move it to Contract for shared vocabularies or mark it [DomainOnly].", ContractArea);

    internal static readonly DiagnosticDescriptor DomainOnlyOutsideDomain = Create("DRNENUM002", "Restrict [DomainOnly] to Domain enums",
        "Enum '{0}' cannot carry [DomainOnly] outside a Domain project; remove [DomainOnly] from public Contract vocabularies.", ContractArea);

    internal static readonly DiagnosticDescriptor DomainOnlyEnumExposed = Create("DRNENUM003", "Do not expose [DomainOnly] enums publicly",
        "Public contract or endpoint '{0}' exposes [DomainOnly] enum '{1}'; move the enum to Contract and remove [DomainOnly] before exposing it.", ContractArea);

    internal static readonly DiagnosticDescriptor EnumZeroRequiresAllowZero = Create("DRNENUM004", "Require [AllowZero] for enum zero values",
        "Enum '{0}' defines zero member '{1}' without [AllowZero]. Start members at a nonzero value or mark the enum with [AllowZero].", ContractArea);

    internal static readonly DiagnosticDescriptor AllowZeroWithoutZeroMember = Create("DRNENUM005", "Require a named zero member with [AllowZero]",
        "Enum '{0}' is marked with [AllowZero] but defines no named member with value 0. Remove [AllowZero] or define a named zero member.", ContractArea);
    private const string ArchitectureArea = "Architecture";
    private const string MigrationsArea = "Migrations";
    private const string ExceptionsArea = "Exceptions";

    internal static readonly DiagnosticDescriptor IncompleteContract = Create("DRNARCHITECTURE001", "Review incomplete public contract inspection",
        "Inspection of public contract member '{0}' reached the analysis limit before verifying that its public model is free of persistence and domain types.", ConfigurationArea);

    internal static readonly DiagnosticDescriptor Contract = Create("DRNARCHITECTURE002", "Keep external contracts free of persistence types",
        "Public contract member '{0}' exposes persistence/domain type '{1}'; expose an explicit Contract Request/Response or value model.", ContractArea);

    internal static readonly DiagnosticDescriptor Reference = Create("DRNARCHITECTURE003", "Remove a forbidden direct reference",
        "Direct {0} reference from '{1}' to '{2}' violates the declared role matrix; remove it or document the exact exception.", ArchitectureArea);

    internal static readonly DiagnosticDescriptor ImplementationRole = Create("DRNARCHITECTURE004", "Place the implementation in its owning layer",
        "Implementation '{0}' ({1}) belongs to role '{2}'.", ArchitectureArea);

    internal static readonly DiagnosticDescriptor ForbiddenDependency = Create("DRNARCHITECTURE005", "Remove a forbidden layer dependency",
        "Type '{0}' in layer '{1}' uses {2} APIs. Remove this dependency.", ArchitectureArea);

    internal static readonly DiagnosticDescriptor RequiredReference = Create("DRNARCHITECTURE006", "Add a required direct reference",
        "Project '{0}' requires a direct project reference to its '{1}' layer.", ArchitectureArea);

    internal static readonly DiagnosticDescriptor MissingOwnership = Create("DRNOWNERSHIP001", "Declare business subdomain ownership",
        "'{0}' needs an unambiguous subdomain; use the conventional namespace or declare its owner.", OwnershipArea);

    internal static readonly DiagnosticDescriptor CrossSubdomain = Create("DRNOWNERSHIP002", "Justify cross-subdomain entity use",
        "'{0}' in subdomain '{1}' references entity '{2}' from '{3}'. Use the entity's identifier or a contract from '{3}'.", OwnershipArea);

    internal static readonly DiagnosticDescriptor DomainSubdomain = Create("DRNOWNERSHIP003", "Establish the infrastructure's Domain subdomain",
        "Infrastructure '{0}' requires populated Domain subdomain '{1}'.", ArchitectureArea);

    internal static readonly DiagnosticDescriptor DomainSubdomainRequired = Create("DRNOWNERSHIP004", "Establish at least one Domain subdomain",
        "Domain project '{0}' requires at least one populated subdomain folder.", OwnershipArea);

    internal static readonly DiagnosticDescriptor Placement = Create("DRNSTRUCTURE001", "Align namespace and logical placement",
        "'{0}' belongs in '{1}' with namespace '{2}'; current logical placement conflicts.", StructureArea);

    internal static readonly DiagnosticDescriptor ContextName = Create("DRNSTRUCTURE002", "Context name must have a nonempty Context stem",
        "Context '{0}' must have a nonempty name ending in 'Context'.", ContextArea);

    internal static readonly DiagnosticDescriptor Destination = Create("DRNSTRUCTURE003", "Declare conventional context destinations",
        "Context '{0}' has no valid {1} destination. Set {1}=\"{2}\" on its DrnConventionContext item, or provide a Reason for an alternate destination.", StructureArea);

    internal static readonly DiagnosticDescriptor RoleSuffix = Create("DRNSTRUCTURE004", "Use the semantic role suffix",
        "Rename '{0}' to end with '{1}' and include a name before the suffix.", StructureArea);

    internal static readonly DiagnosticDescriptor AggregatePlacement = Create("DRNSTRUCTURE005", "Place aggregates in their named directory and namespace",
        "Place aggregate '{0}' in a folder named '{1}' and a namespace ending in '.{1}'.", StructureArea);

    internal static readonly DiagnosticDescriptor InfrastructureFolder = Create("DRNSTRUCTURE006", "Use the dedicated infrastructure folder",
        "Place '{0}' in a folder named '{1}' and a namespace ending in '.{1}'.", StructureArea);

    internal static readonly DiagnosticDescriptor ContextOptionsName = Create("DRNSTRUCTURE007", "Name options for their context",
        "Rename options '{0}' to start with '{1}' and end with 'Options'. An 'Attribute' suffix may follow. Recommended name: '{1}NpgsqlDbContextOptionsAttribute'.", ContextArea);

    internal static readonly DiagnosticDescriptor DomainFolderStructure = Create("DRNSTRUCTURE008", "Keep Domain grouping folders folders-only",
        "'{0}' violates Domain folder rules: {1}.", StructureArea);

    internal static readonly DiagnosticDescriptor ContextConstructor = Create("DRNPERSISTENCE001", "Provide supported context construction",
        "Context '{0}' needs the options constructor required by its declared registration/design-time path.", ContextArea);

    internal static readonly DiagnosticDescriptor Provider = Create("DRNPERSISTENCE002", "Establish effective Npgsql convention",
        "Context '{0}' has no established Npgsql setup on its declared DRN configuration path; provide or declare the supported setup.", ContextArea);

    internal static readonly DiagnosticDescriptor BaseModel = Create("DRNPERSISTENCE003", "Preserve required base model configuration",
        "Override '{0}' has a proven path that omits the required base model configuration.", ContextArea);

    internal static readonly DiagnosticDescriptor Persistence = Create("DRNPERSISTENCE004", "Make persistence assignment consistent",
        "Assignment of '{0}' to context '{1}' conflicts with '{2}'; resolve or declare the intended relationship.", PersistenceArea);

    internal static readonly DiagnosticDescriptor Configuration = Create("DRNPERSISTENCE005", "Make declared configuration discoverable",
        "Configuration '{0}' for '{1}' is not discoverable by context '{2}'; use its configuration namespace/assembly or an explicit supported application path.", PersistenceArea);

    internal static readonly DiagnosticDescriptor Migration = Create("DRNPERSISTENCE006", "Align migrations and snapshot context identity",
        "Migration artifact '{0}' identifies context '{1}' inconsistently with declared destination '{2}'.", MigrationsArea);

    internal static readonly DiagnosticDescriptor ContextContract = Create("DRNPERSISTENCE007", "Implement the application persistence context contract",
        "Application context '{0}' must implement IDrnContext or declare a narrow, justified exception.", ContextArea);

    internal static readonly DiagnosticDescriptor DbSetAggregate = Create("DRNPERSISTENCE008", "Expose aggregate roots through DbSet properties",
        "DbSet property '{0}' on context '{1}' exposes '{2}', which must derive from AggregateRoot.", PersistenceArea);

    internal static readonly DiagnosticDescriptor ContextOptionsRequired = Create("DRNPERSISTENCE009", "Declare context options on the context",
        "Context '{0}' must directly declare a custom attribute derived from NpgsqlDbContextOptionsAttribute. Recommended name: '{0}NpgsqlDbContextOptionsAttribute'.", ContextArea);

    internal static readonly DiagnosticDescriptor Repository = Create("DRNREPOSITORY001", "Align repository aggregate and context",
        "Repository '{0}' binds aggregate '{1}' inconsistently with contract or context '{2}'.", RepositoryArea);

    internal static readonly DiagnosticDescriptor Service = Create("DRNREPOSITORY002", "Align SourceKnown service binding",
        "Service '{0}' has inconsistent aggregate/repository binding '{1}'.", ApplicationArea);

    internal static readonly DiagnosticDescriptor AggregateContract = Create("DRNREPOSITORY003", "Declare the aggregate repository interface",
        "Aggregate '{0}' has no matching repository interface in Domain subdomain '{1}'. Declare or move an interface extending ISourceKnownRepository<{0}> into the same namespace and folder as '{0}'.", RepositoryArea);

    internal static readonly DiagnosticDescriptor AggregateRepository = Create("DRNREPOSITORY004", "Implement the aggregate repository in infrastructure",
        "Aggregate '{0}' has no matching repository in Infra subdomain '{1}'. Add a concrete SourceKnownRepository implementation of its Domain repository interface.", RepositoryArea);

    internal static readonly DiagnosticDescriptor Registration = Create("DRNDEPENDENCYINJECTION001", "Establish effective service/repository registration",
        "'{0}' has no effective registration for declared service '{1}' on the supported composition path.", RegistrationArea);

    internal static readonly DiagnosticDescriptor LifetimeRequired = Create("DRNDEPENDENCYINJECTION002", "Declare a DI lifetime for paired implementations",
        "'{0}' must register '{1}' through a matching LifetimeAttribute. Choose [Scoped<{1}>], [Transient<{1}>], or [Singleton<{1}>] for its intended lifetime.", RegistrationArea);

    internal static readonly DiagnosticDescriptor ModuleScan = Create("DRNDEPENDENCYINJECTION003", "Declare a project module with an attribute scan",
        "Project '{0}' needs a public static *Module class with a public static method accepting and returning IServiceCollection. Call services.AddServicesWithAttributes() on that parameter to scan this project's assembly.", RegistrationArea);

    internal static readonly DiagnosticDescriptor InvalidInput = Create("DRNCONVENTIONINPUT001", "Invalid or incomplete convention input",
        "Convention input '{0}' is {1}. {2}.", ConfigurationArea);

    internal static readonly DiagnosticDescriptor Exception = Create("DRNCONVENTIONEXCEPTION001", "Make observable exceptions narrow and reasoned",
        "Exception for '{0}' is {1}; provide an exact target, meaningful reason and required ownership metadata.", ExceptionsArea);

    internal static ImmutableArray<DiagnosticDescriptor> All { get; } =
    [
        IncompleteContract, Contract, Reference, ImplementationRole, ForbiddenDependency, RequiredReference,
        MissingOwnership, CrossSubdomain, DomainSubdomain, DomainSubdomainRequired,
        Placement, ContextName, Destination, RoleSuffix, AggregatePlacement, InfrastructureFolder, ContextOptionsName, DomainFolderStructure,
        ContextConstructor, Provider, BaseModel, Persistence, Configuration, Migration, ContextContract, DbSetAggregate, ContextOptionsRequired,
        Repository, Service, AggregateContract, AggregateRepository,
        Registration, LifetimeRequired, ModuleScan, InvalidInput, Exception,
        EntityDto, DtoFolder, DtoClassification, DtoMessagePlacement, DtoExposure, ExplicitContractMessage,
        DomainEnumRequiresMarker, DomainOnlyOutsideDomain, DomainOnlyEnumExposed, EnumZeroRequiresAllowZero, AllowZeroWithoutZeroMember
    ];

    private static DiagnosticDescriptor Create(string id, string title, string message, string area) =>
        new(id, title, message, "DRN.Conventions." + area, DiagnosticSeverity.Warning, true,
            helpLinkUri: "https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/CONVENTION-DIAGNOSTICS.md");
}
