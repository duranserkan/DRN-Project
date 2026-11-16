using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionDiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor InvalidInput = Create("DRNCV001", "Invalid or incomplete convention input",
        "Convention input '{0}' is {1}. {2}.", "Configuration");

    internal static readonly DiagnosticDescriptor MissingOwnership = Create("DRNCV002", "Declare business subdomain ownership",
        "'{0}' needs an unambiguous subdomain; use the conventional namespace or declare its owner.", "Ownership");

    internal static readonly DiagnosticDescriptor Placement = Create("DRNCV003", "Align namespace and logical placement",
        "'{0}' belongs in '{1}' with namespace '{2}'; current logical placement conflicts.", "Structure");

    internal static readonly DiagnosticDescriptor ContextName = Create("DRNCV004", "Context name must have a nonempty Context stem",
        "Context '{0}' must have a nonempty name ending in 'Context'.", "Context");

    internal static readonly DiagnosticDescriptor ContextConstructor = Create("DRNCV005", "Provide supported context construction",
        "Context '{0}' needs the options constructor required by its declared registration/design-time path.", "Context");

    internal static readonly DiagnosticDescriptor Provider = Create("DRNCV006", "Establish effective Npgsql convention",
        "Context '{0}' has no established Npgsql setup on its declared DRN configuration path; provide or declare the supported setup.", "Context");

    internal static readonly DiagnosticDescriptor BaseModel = Create("DRNCV007", "Preserve required base model configuration",
        "Override '{0}' has a proven path that omits the required base model configuration.", "Context");

    internal static readonly DiagnosticDescriptor Persistence = Create("DRNCV008", "Make persistence assignment consistent",
        "Assignment of '{0}' to context '{1}' conflicts with '{2}'; resolve or declare the intended relationship.", "Persistence");

    internal static readonly DiagnosticDescriptor Configuration = Create("DRNCV009", "Make declared configuration discoverable",
        "Configuration '{0}' for '{1}' is not discoverable by context '{2}'; use its configuration namespace/assembly or an explicit supported application path.", "Persistence");

    internal static readonly DiagnosticDescriptor Destination = Create("DRNCV010", "Declare conventional context destinations",
        "Context '{0}' has no valid {1} destination. Set {1}=\"{2}\" on its DrnConventionContext item, or provide a Reason for an alternate destination.", "Structure");

    internal static readonly DiagnosticDescriptor Repository = Create("DRNCV011", "Align repository aggregate and context",
        "Repository '{0}' binds aggregate '{1}' inconsistently with contract or context '{2}'.", "Repository");

    internal static readonly DiagnosticDescriptor Registration = Create("DRNCV012", "Establish effective service/repository registration",
        "'{0}' has no effective registration for declared service '{1}' on the supported composition path.", "Registration");

    internal static readonly DiagnosticDescriptor Service = Create("DRNCV013", "Align SourceKnown service binding",
        "Service '{0}' has inconsistent aggregate/repository binding '{1}'.", "Application");

    internal static readonly DiagnosticDescriptor Contract = Create("DRNCV014", "Keep external contracts free of persistence types",
        "Public contract member '{0}' exposes persistence/domain type '{1}'; expose an approved DTO or value model.", "Contract");

    internal static readonly DiagnosticDescriptor Reference = Create("DRNCV015", "Respect direct role references",
        "Direct {0} reference from '{1}' to '{2}' violates the declared role matrix; remove it or document the exact exception.", "Architecture");

    internal static readonly DiagnosticDescriptor ImplementationRole = Create("DRNCV016", "Place semantic implementations in their owning role",
        "Implementation '{0}' ({1}) belongs to role '{2}'.", "Architecture");

    internal static readonly DiagnosticDescriptor Migration = Create("DRNCV017", "Align migrations and snapshot context identity",
        "Migration artifact '{0}' identifies context '{1}' inconsistently with declared destination '{2}'.", "Migrations");

    internal static readonly DiagnosticDescriptor Exception = Create("DRNCV018", "Make observable exceptions narrow and reasoned",
        "Exception for '{0}' is {1}; provide an exact target, meaningful reason and required ownership metadata.", "Exceptions");

    internal static readonly DiagnosticDescriptor CrossSubdomain = Create("DRNCV019", "Justify cross-subdomain entity use",
        "'{0}' in subdomain '{1}' references entity '{2}' from '{3}'. Use the entity's identifier or a contract from '{3}'.", "Ownership");

    internal static readonly DiagnosticDescriptor ContextContract = Create("DRNCV020", "Implement the application persistence context contract",
        "Application context '{0}' must implement IDrnContext or declare a narrow, justified exception.", "Context");

    internal static readonly DiagnosticDescriptor RequiredReference = Create("DRNCV021", "Reference the required application layer directly",
        "Project '{0}' requires a direct project reference to its '{1}' layer.", "Architecture");

    internal static readonly DiagnosticDescriptor DomainSubdomain = Create("DRNCV022", "Establish the infrastructure's Domain subdomain",
        "Infrastructure '{0}' requires populated Domain subdomain '{1}'.", "Architecture");

    internal static readonly DiagnosticDescriptor DbSetAggregate = Create("DRNCV023", "Expose aggregate roots through DbSet properties",
        "DbSet property '{0}' on context '{1}' exposes '{2}', which must derive from AggregateRoot.", "Persistence");

    internal static readonly DiagnosticDescriptor RoleSuffix = Create("DRNCV024", "Use the semantic role suffix",
        "Rename '{0}' to end with '{1}' and include a name before the suffix.", "Structure");

    internal static readonly DiagnosticDescriptor AggregatePlacement = Create("DRNCV025", "Place aggregates in their named directory and namespace",
        "Place aggregate '{0}' in a folder named '{1}' and a namespace ending in '.{1}'.", "Structure");

    internal static readonly DiagnosticDescriptor AggregateContract = Create("DRNCV026", "Declare the aggregate repository interface",
        "Aggregate '{0}' has no matching repository interface in Domain subdomain '{1}'. Declare or move an interface extending ISourceKnownRepository<{0}> into the same namespace and folder as '{0}'.", "Repository");

    internal static readonly DiagnosticDescriptor AggregateRepository = Create("DRNCV027", "Implement the aggregate repository in infrastructure",
        "Aggregate '{0}' has no matching repository in Infra subdomain '{1}'. Add a concrete SourceKnownRepository implementation of its Domain repository interface.", "Repository");

    internal static readonly DiagnosticDescriptor InfrastructureFolder = Create("DRNCV028", "Use the dedicated infrastructure folder",
        "Place '{0}' in a folder named '{1}' and a namespace ending in '.{1}'.", "Structure");

    internal static readonly DiagnosticDescriptor ContextOptionsName = Create("DRNCV029", "Name options for their context",
        "Rename options '{0}' to start with '{1}' and end with 'Options'. An 'Attribute' suffix may follow. Recommended name: '{1}NpgsqlDbContextOptionsAttribute'.", "Context");

    internal static readonly DiagnosticDescriptor ContextOptionsRequired = Create("DRNCV030", "Declare context options on the context",
        "Context '{0}' must directly declare a custom attribute derived from NpgsqlDbContextOptionsAttribute. Recommended name: '{0}NpgsqlDbContextOptionsAttribute'.", "Context");

    internal static readonly DiagnosticDescriptor LifetimeRequired = Create("DRNCV031", "Declare a DI lifetime for paired implementations",
        "'{0}' must register '{1}' through a matching LifetimeAttribute. Choose [Scoped<{1}>], [Transient<{1}>], or [Singleton<{1}>] for its intended lifetime.", "Registration");

    internal static readonly DiagnosticDescriptor ModuleScan = Create("DRNCV032", "Declare a project module with an attribute scan",
        "Project '{0}' needs a public static *Module class with a public static method accepting and returning IServiceCollection. Call services.AddServicesWithAttributes() on that parameter to scan this project's assembly.", "Registration");

    internal static ImmutableArray<DiagnosticDescriptor> All { get; } =
    [
        InvalidInput, MissingOwnership, Placement, ContextName, ContextConstructor, Provider, BaseModel,
        Persistence, Configuration, Destination, Repository, Registration, Service, Contract, Reference,
        ImplementationRole, Migration, Exception, CrossSubdomain, ContextContract, RequiredReference, DomainSubdomain, DbSetAggregate,
        RoleSuffix, AggregatePlacement, AggregateContract, AggregateRepository, InfrastructureFolder, ContextOptionsName, ContextOptionsRequired, LifetimeRequired, ModuleScan
    ];

    private static DiagnosticDescriptor Create(string id, string title, string message, string area) =>
        new(id, title, message, "DRN.Conventions." + area, DiagnosticSeverity.Warning, true,
            helpLinkUri: "https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/CONVENTION-DIAGNOSTICS.md");
}
