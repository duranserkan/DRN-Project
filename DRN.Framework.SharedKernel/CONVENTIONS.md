# SourceKnown application conventions

Application projects receive ownership, placement and boundary warnings by default. Conventions do not configure EF, allocate entity IDs, register services or authorize endpoints. Existing SourceKnown identity, classification and authorization diagnostics remain independent of convention activation.

Provider and composition analysis are bounded. Quiet analysis does not establish model, registration, authorization or performance correctness. Validate those properties through the application's runtime and model checks.

## Project identity by convention

Project names determine roles without custom activation or identity properties. Match whole dot-delimited tokens case-insensitively:

| Role | Accepted tokens |
|---|---|
| Domain | `Domain` |
| Contract | `Contract`, `Contracts` |
| Infra | `Infra`, `Infrastructure` |
| Application | `Application` |
| Hosted | `Hosted`, `Host`, `Hosting` |
| SharedKernel | `SharedKernel` |
| Utils | `Utils` |

A single role token can appear anywhere in the name. `Infra`, `Company.Shop.Infra` and `Company.Shop.Infrastructure.Postgres` all have role Infra. No application prefix or shared naming root is required. `DomainHelpers` does not contain a role token. Multiple role tokens, such as `Shop.Domain.Infra`, are ambiguous and report DRNCV001 instead of selecting one. Project and assembly names identify individual projects and assemblies. Standard SDK `RootNamespace` controls namespace placement, not application membership.

Projects with a whole `Framework`, `Test`, `Tests`, `Testing`, `Analyzer`, `Analyzers`, `CodeFixes`, `Generator` or `Generators` token are outside application checks. Unknown names also remain outside these checks. This keeps framework and test-support projects separate without project-specific opt-outs.

Build targets supply convention inputs and basic project metadata for C# projects. The analyzer uses inferred identity to decide which application rules apply. The input fragment's `Version("1")` is a data schema version, independent of project identity. Existing identity, classification and authorization analyzers remain active independently.

Review new warnings when upgrading, particularly with warnings-as-errors. Use standard `.editorconfig` diagnostic severity settings or narrow reasoned suppressions for intentional exceptions. No custom activation or identity property is needed.

Split roles use `DrnConventionRole` items with nonoverlapping `NamespacePrefix` and `LogicalPathPrefix`. Review direct edges against each applicable role, without combining permissions.

Placement uses `Compile`/`Link` logical paths, ordinal case and normalized separators. Physical source paths inside the project are normalized to project-relative paths. Unlinked files under intermediate-output directories do not produce logical-path records, regardless of whether those directories are relative, rooted or parent-relative. They remain compiled and supply semantic evidence. Explicit `Link` metadata is preserved for validation. Absolute logical paths, traversal and external source without a valid link are invalid. Missing paths defer path checks. Organization warnings target handwritten code. Cross-owner checks include inherited signatures and generated uses when a handwritten partial/member or exact ownership input provides an actionable location. Fully generated consumers without such a location remain unreported. Migration identity checks include generated artifacts.

## Ownership and persistence

Concrete application-owned EF `DbContext` subclasses must implement `IDrnContext` when conventions are enabled. An inherited implementation counts. No particular `DrnContext` base is required, and abstract bases receive no duplicate interface-policy warning. A specialized context can use a narrow, justified DRNCV020 exception. External context declarations are not rewritten or certified. Implementing the interface alone does not establish provider setup, model validation, lifecycle hooks or every DRN runtime safeguard.

Business subdomain checks apply to Domain types and entities, persistence contexts/configurations, repository and service bindings, DI-registered Application/Infra services, and SourceKnown controllers. Nested helper types retain their owning type's boundary. Ordinary MVC controllers, Razor page models, tag helpers and unclassified helpers do not acquire business ownership from navigation or utility namespaces. Razor page models may use domain entities; their folders represent navigation. Public API contract checks still apply to MVC controllers, and project dependency and implementation-role checks remain independent. Existing DRN0026 enforcement still applies to SourceKnown controllers.

The first namespace segment beneath the role owns the subdomain. Hosted may first use `Controllers` or `Pages`, Application may use `Services`. `Shop.Domain.Sales.Orders.Order` and `Shop.Application.Services.Sales.OrderService` belong to Sales. Feature folders do not change ownership.

Ownership retains the declaring assembly and symbol. Bound Infra, Application or Hosted use of a referenced Domain entity can share its subdomain across naming prefixes. Two Domain assemblies remain distinct owners even when their subdomain names match.

Use `[SourceKnownSubdomain("Identity", Reason = "Authentication account ownership")]` for technical aliases. The attribute belongs to `DRN.Framework.SharedKernel.Attributes`. It does not waive placement warnings.

Assembly-level `SourceKnownSubdomain(typeof(ExternalType), "Owner", Reason = "...")` identifies one external type. It cannot redefine an established referenced owner.

`SourceKnownPersistence(typeof(Order))` declares context membership evidence. `Inclusion = SourceKnownPersistenceInclusion.Exclude` declares exclusion. Neither changes EF's model or suppresses DRNCV019. Missing DbSets do not prove exclusion. Attribute-only mappings need no configuration class or suppression.

`SalesReadContext` and `SalesWriteContext` may share Order within Sales. Context stems differing from their namespace owner need reasoned explicit ownership. Declare isolated destinations where intended. No runtime read-only guarantee or storage change is implied.

## Declared inputs

| Item | Required relationship and optional evidence |
|---|---|
| `DrnConventionContext` | Exact context metadata name. Conventional `Repositories` and `Migrations` destinations are inferred from namespace and logical source homes. Declare destinations for alternate or ambiguous layouts, with `Reason` for alternatives. Optional `Subdomain`, `MigrationsAssembly`, `Factory`, `Provider`, `Reason`. Custom construction/provider declarations need a review reason and runtime/model evidence. |
| `DrnConventionOwnership` | Exact type, `Subdomain`, optional `Kind` (`Entity` or `Value`) and `Reason`. Persistence types cannot become neutral values. |
| `DrnConventionConfiguration` | Exact configuration type and `Context`. Records intended association without requiring a configuration class for every entity. |
| `DrnConventionRegistration` | Exact implementation and `Service`, optional `Key`. `Reason` documents a reviewed custom/external path whose runtime behavior still needs validation. |
| `DrnConventionException` | Exact diagnostic, `SymbolTarget`, `Reason`, `Owner`, `Review` (`yyyy-MM-dd`). Optional exact `Entity` and `Context` narrow an exception further. |

Track destinations before repositories/migrations exist, without fake files. Separate migration assemblies require association and compatibility review.

Convention input is a bounded data-only assembly-attribute fragment, with at most 1,048,576 characters and 16,384 records. It must contain exactly one `Version("1")`. Literal strings, parentheses and literal-string concatenation are supported. Identical non-version records are idempotent. Contradictory records for the same unique target are invalid. Malformed or unsupported supplied input prevents refactorings from choosing default convention paths.

Producers export basic project identity and declared type ownership/classification as assembly metadata. Referencing consumers infer roles from exported project names, falling back to the actual resolved assembly name when that metadata is absent. Package IDs do not establish project roles. Declaring-assembly facts survive reference assemblies. A consumer cannot relabel an established producer owner or entity/value classification. Contradictory exported facts are incomplete input, not a reason to select the first value. Each consumer's own project name determines which application checks apply.

## Progressive dependency rules

The layers present in an application determine its required direct project references:

| Existing layer | Minimum required direct project references |
|---|---|
| Hosted | At least one Application and at least one Infra |
| Infra | At least one Domain |
| Domain | At least one Contract |
| Application, Contract, SharedKernel, Utils | No additional required layer |

These are directional `ProjectReference` requirements. At least one direct project of each required role satisfies the rule. There is no naming-prefix restriction, ordering requirement or upper limit. Multiple Hosted projects may share Infra, Infra may reference multiple Domain projects, and Domain may reference multiple Contract projects. A transitive reference or package dependency does not satisfy a direct project requirement. Every additional reference still has to obey the allowed direction. DRNCV021 identifies a missing required role. Absence checks require complete, valid reference inventory. Incomplete evidence is not proof of a missing dependency.

Every Infra project must reference Domain projects that collectively contain at least one populated subdomain namespace. For example, a type in `Shop.Domain.Sales` establishes Sales in that declaring assembly. Subdomain evidence is collected across directly referenced Domain projects. Concrete Infra contexts and repositories must have a matching Domain subdomain. Same-named subdomains in different assemblies retain distinct ownership identities. DRNCV022 reports missing Domain subdomain evidence. An empty directory or namespace with no types is insufficient compiler evidence. Missing required references are reported before dependent subdomain checks.

Allowed references remain separate from required references. Domain may reference Contract, Utils and SharedKernel. Contract may reference SharedKernel. Infra may reference Domain, Contract, Utils and SharedKernel. Application may additionally reference Infra. Hosted may additionally reference Application. Utils may reference SharedKernel. SharedKernel has no outward application-layer dependencies.

SharedKernel is a foundation role for shared contracts and abstract entity, DTO and service bases. It does not become an alternative home for concrete application entities, services, persistence implementations or transport. Existing semantic placement and public-contract rules still apply.

## Service registration evidence

DRNCV012 checks explicit implementation/service/key demands and eligible services on an observed `AddServicesWithAttributes()` or `AddServicesWithAttributes(typeof(Marker).Assembly)` path. Ordinary Application orchestration and Infra repository implementations need no SourceKnown base or explicit registration item.

The supported implicit boundary is a concrete, closed Application or Infra implementation of a paired `I{ImplementationName}` interface in Application, Domain or Infra. The interface must declare or inherit instance operations, or inherit a recognized service/repository contract. Required parameters of a single public constructor on these implementations, lifetime-attributed services and MVC/Razor transports also establish service relationships when exactly one eligible implementation is available. This includes concrete self-services and literal string `[FromKeyedServices]` selectors. Optional parameters and ambiguous constructor or implementation choices do not establish exact demands. Entities, declared neutral values, DTOs, EF configurations, contexts and configuration-bound types are excluded from implicit implementation discovery.

A paired interface without an observed key demand needs a matching registration under any supported key. Explicit demands and observed constructor dependencies require their exact key or unkeyed registration. A keyed empty string differs from unkeyed registration; the optional project-item `Key` is unkeyed when omitted or empty. Effective inherited lifetime attributes count according to DRN scan behavior. Supported manual registrations are direct generic `AddScoped`, `AddTransient`, `AddSingleton` and their keyed counterparts with implementation types, including concrete self-registration. Factory delegates, non-string keys, custom modules/helpers and unobserved composition are not absence proofs. Unknown composition defers the warning.

Names alone, data-only interfaces, unpaired unused interfaces and arbitrary helper classes do not establish service intent. Such cases need explicit demands or runtime composition review. The analyzer cannot infer every intended service. Use a narrow justified suppression when registration is intentionally unnecessary, and do not add duplicate manual registrations for attributed services.

Eligible `HostedServiceAttribute` implementations satisfy unkeyed `IHostedService` demands. Unrelated external calls such as `Math.Abs` beside a scan do not make composition unknown.

Custom nongeneric lifetime bindings can be proven through source-visible explicit constructor chains forwarding `typeof(...)` or type-valued attribute arguments to `LifetimeAttribute`'s service type parameter. Arbitrary expressions and unavailable bodies remain unproven. DRNCV012 still defers custom key semantics.

When DRNCV031 reports a binding failure, DRNCV012 does not repeat it for the same implementation/interface pair. DRNCV012 still checks other service demands and key mismatches after a valid binding. Neither rule proves that an attribute scan is called at runtime; DRNCV012 defers when no supported scan is observed.

DRNCV032 requires Hosted, Application, Infra and Utils projects to declare a public top-level nongeneric static `*Module` class. A public static nongeneric method must accept and return `IServiceCollection` and directly call the DRN attribute scan on that parameter for its own assembly. Parameterless scans and explicit `typeof(LocalType).Assembly` scans count. Nested callbacks and scans of other assemblies do not count. Domain, Contract, SharedKernel, framework and test projects are excluded. This checks the module's structure, not whether the host invokes it.

DRNCV031 independently requires one lifetime attribute bound to the matching interface on concrete, closed Application/Infra service implementations paired by exact interface name (`AnswerRepository : IAnswerRepository`). Generic and keyed lifetime derivatives count, including custom derivatives. Direct attributes take precedence over inherited attributes. The binding must match the exact interface symbol and generic arguments; self-registration, other service bindings and multiple selected lifetime attributes fail the rule. Use `[Scoped<IAnswerRepository>]`, `[Transient<IAnswerRepository>]` or `[Singleton<IAnswerRepository>]` according to dependency lifetime requirements. Manual registration and unknown composition do not waive this binding rule. Existing service eligibility exclusions keep entities, DTOs, neutral values, contexts, configurations, bound options, controllers and page models outside its scope. Exact justified convention exceptions remain available.

## Diagnostic catalog

The [full diagnostic reference](CONVENTION-DIAGNOSTICS.md) provides exact messages, applicability, examples, remedies, exceptions and fix risks for every rule.

DRNCV rules default to Warning in enabled application projects, affecting warning-as-error consumers. No fix renames contexts, moves configurations or rewrites migrations.

| ID | Why and ordinary remedy | Valid / invalid example | Exception and limit |
|---|---|---|---|
| DRNCV001 | Supply unambiguous project identity and consistent facts. | `Shop.Infrastructure.Sql` / `Shop.Domain.Infra`, conflicting owners or invalid logical path. | Unknown names do not activate application checks. Missing evidence is not guessed. |
| DRNCV002 | Give relevant entities and bound services a clear owner. | `Domain.Sales.Order` / unowned `Domain.Order`. | Reasoned exact owner, including technical Identity. Abstract/open intermediates are excluded. |
| DRNCV003 | Align namespace and logical source home. | `Sales/Order.cs` under `Domain.Sales` / divergent partial home. | Exact placement exception or split-role mapping. No automatic move. |
| DRNCV004 | Make context names recognizable without changing ownership. | `SalesReadContext` / `Store` or `Context`. | Narrow legacy operational exception. Plain DbContext subclasses count; unrelated name lookalikes do not. |
| DRNCV005 | Provide the options constructor for a supported construction path. | Public `DbContextOptions<SalesContext>` constructor / DRN context with only a parameterless constructor. | Reasoned custom factory. Existing equivalent generic constraint errors are not duplicated. Guarded additive fix only. |
| DRNCV006 | Preserve Npgsql on the selected construction path. | Forwarded convention options / direct DRN options constructor discards them for null or fresh empty options. | Custom OnConfiguring/factory/helper paths remain unknown. Empty local options delegates do not prove absence. |
| DRNCV007 | Retain required base model setup. | `base.OnModelCreating(model)` / empty override. | Reasoned replacement with integration evidence. Current proof covers straight-line bodies and bounded local forwarding. Arbitrary flow/helpers remain unknown. |
| DRNCV008 | Resolve contradictory persistence declarations. | One explicit inclusion / simultaneous include and exclude. | Shared/external membership is valid. No complete EF model inference or missing-DbSet warning. |
| DRNCV009 | Make intended configurations discoverable. | Same assembly, exact/child context namespace / declared sibling configuration with a proven default scan. | Explicit associations and unambiguous context-area/membership evidence count. Ambiguous CQRS associations need selection. Attribute-only code is valid. |
| DRNCV010 | Infer conventional repository and migration destinations. Require explicit destinations for alternate or ambiguous layouts. Validate explicit destinations even without artifacts. | `Sales/Repositories`, `Sales/Migrations` without metadata / conflicting logical homes without a declaration. | Reasoned alternate output. No directory scanning or fake initial migration. |
| DRNCV011 | Keep repository binding compatible with persistence. | Repository uses included Order / repository targets explicitly excluded Order. | Exact intentional exception with model evidence. Current reporting focuses on explicit exclusion and supported Ignore evidence. |
| DRNCV012 | Establish effective registration for explicit or inferred service relationships. | Matching supported registration / eligible service on an observed scan without matching lifetime binding. | Reasoned custom registration. Eligibility and unknown composition limits are described above. |
| DRNCV013 | Keep service and repository aggregate bindings consistent. | Order service returns Order repository / resolved incompatible repository expression. | Exact reviewed adaptation. Current proof is bounded to supported repository expressions. |
| DRNCV014 | Keep external contracts free of persistence objects. | Dto/Guid / public collection containing an entity. | Approved neutral value model. Dto mapping constructors may accept their source entity. Complex or external graphs remain bounded. |
| DRNCV015 | Respect direct application-role edges. | Application to Infra / Domain to Infra. | Exact reasoned integration edge. Direct provenance and resolved assembly/project identity are required. Package IDs do not establish roles. |
| DRNCV016 | Put semantic implementations in their owning role. | Context in Infra, controller in Hosted / context or EntityTypeBuilder use in Domain. | Exact reasoned role exception. Known declaration and bound-use families count. Lightweight EF mapping attributes remain valid. |
| DRNCV017 | Preserve declared migration/context association. | Matching context/assembly/destination / known artifact in conflicting destination. | Explicit separate migration assembly. No EF schema equivalence claim or historical rewrite. |
| DRNCV018 | Keep observable exceptions exact and reviewable. | Exact target with reason / wildcard, blank reason or due review. | Standard channels below. Source fingerprints bind reviewed content. External switches cannot be universally audited. |
| DRNCV019 | Make intentional cross-subdomain entity coupling visible. | Same-owner CQRS membership / Sales code uses an Inventory entity without a narrow exception. | Use owning contracts or identifiers, correct misplaced ownership/code, or justify the exact reuse. Membership alone is not an exception. |
| DRNCV020 | Require IDrnContext on concrete application persistence contexts. | Inherited interface implementation / concrete DbContext without IDrnContext. | Narrow specialized-context exception. Abstract bases are excluded. No runtime-safeguard certification. |
| DRNCV021 | Supply at least one direct project reference per required role. | Hosted directly references Application and shared Infra / Infra available only transitively. | No application-root restriction. Complete valid reference inventory is required for absence claims. |
| DRNCV022 | Establish Domain subdomains for Infra. | Domain has Sales types and Infra Sales contexts/repositories / empty Domain or unmatched Infra owner. | Types in child namespaces establish subdomains. Empty folders do not. |
| DRNCV023 | Expose aggregate roots through DrnContext DbSet properties. | DbSet of an AggregateRoot descendant / DbSet of a child entity or keyless row. | Includes inherited instance properties. Unresolved generic entities are deferred. Plain DbContext and DrnContextIdentity are outside this rule. |

## Aggregate names and repository pairs

Concrete aggregates use an `Entity` suffix and a matching final namespace and logical directory segment: `OrderEntity` belongs in `QA/Order/OrderEntity.cs` under `<DomainRoot>.QA.Order`. Partial declarations follow the same directory rule. Abstract/open generic helpers are not concrete aggregates. Repository classes and interfaces end in `Repository`, semantic application services and their contracts end in `Service`, and MVC controllers end in `Controller`. Each suffix needs a nonempty stem. Existing context naming remains governed by DRNCV004. Other role folders retain their existing conventions.

Each concrete Domain aggregate requires an interface derived from `ISourceKnownRepository<TEntity>` in the same Domain assembly, namespace and logical folder. For example, `OrderEntity.cs` and `IOrderRepository.cs` both belong in `QA/Order`. Infra must provide a concrete `SourceKnownRepository<TContext,TEntity>` implementing that interface for aggregates assigned to its contexts. Assignment evidence includes DbSets, explicit persistence inclusion and supported configuration application/default discovery. Explicit exclusion or Ignore removes the obligation for that context. An aggregate used by multiple contexts needs one implementation in that Infra project. Merely referencing a Domain assembly does not require repositories for all its aggregates.

| Diagnostic | Requirement |
|---|---|
| DRNCV024 | Use a nonempty Entity, Repository, Service, Controller or Config name for the corresponding semantic role. |
| DRNCV025 | Match the aggregate namespace and directory to its name without Entity. |
| DRNCV026 | Declare the aggregate's repository interface in the same Domain namespace and logical folder. |
| DRNCV027 | Implement the assigned aggregate's Domain repository interface through SourceKnownRepository in Infra. |
| DRNCV028 | Put Infra repository implementations in Repositories, configurations in Configurations, and DRN migrations/snapshots in Migrations. |
| DRNCV029 | Start custom options names with the applied context name and end with Options, before an optional Attribute suffix. |
| DRNCV030 | Directly apply a custom NpgsqlDbContextOptionsAttribute descendant to each concrete, closed DRN context. Inherited options alone do not satisfy this requirement. |
| DRNCV031 | Register each concrete, closed Application/Infra service implementing an exact I{Name} interface through one matching LifetimeAttribute. |
| DRNCV032 | Expose a public static *Module class in Hosted, Application, Infra and Utils projects with an IServiceCollection method calling AddServicesWithAttributes() for its assembly. |

Dedicated folders are the immediate logical parent and final namespace segment. For example, `QA/Configurations/OrderConfig.cs`, `QA/Repositories/OrderRepository.cs` and `QA/Migrations/QAContextModelSnapshot.cs`. Domain interfaces remain beside their aggregates. Compile/Link paths govern placement. Generated migrations are checked through their context association. A reasoned destination override does not itself waive DRNCV028. Custom Npgsql options attributes applied to a DRN context follow `QAContext...Options[Attribute]`. Shared framework defaults are exempt. Options naming does not add an Options folder requirement.

Concrete, closed DRN contexts must directly declare custom options. Prefer `QAContextNpgsqlDbContextOptionsAttribute`, applied as `[QAContextNpgsqlDbContextOptions]`. `NpgsqlDbContext` is recommended within the name. Shared framework defaults are exempt from naming checks but do not replace this direct declaration.

## Narrow exceptions

Correct real violations first. Standard suppression covers the named diagnostic on one symbol. Use pair-specific project evidence to exempt only one consumer/entity relationship.

```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "DRN.Conventions.Ownership", "DRNCV019",
    Justification = "This reconciliation reads the existing inventory aggregate inside the reviewed transaction boundary.")]
public void ReconcileInventory() { }
```

This illustrates suppression placement, not an operation template. Project exceptions use resolvable documentation-comment targets or project-level target tokens:

- `DRNCV021`: `SymbolTarget="<AssemblyName>:Application"`, `<AssemblyName>:Infra`, `<AssemblyName>:Domain`, or `<AssemblyName>:Contract` (for example, `Shop.Domain:Contract`).
- `DRNCV022`: `SymbolTarget="<AssemblyName>:Domain"` when no populated Domain subdomain exists, or the unresolved context/repository `T:...` symbol target.
- `DRNCV032`: `SymbolTarget="<AssemblyName>:Module"`.
- `DRNCV027`: optionally qualify with `Context="Shop.Infra.Sales.SalesContext"` alongside `SymbolTarget` and `Entity`.

```xml
<DrnConventionException Include="DRNCV019"
                        SymbolTarget="M:Shop.Application.Sales.InventoryReconciler.ReconcileInventory"
                        Entity="Shop.Domain.Inventory.Stock"
                        Reason="Reviewed cross-domain reconciliation in one transaction."
                        Owner="Sales team"
                        Review="2027-01-15" />
```

`Review` must be later than today's UTC date. Due reviews refuse the waiver with DRNCV018. No wildcards or unresolved targets. Optional lowercase SHA-256 `Fingerprint` requires valid logical paths for all source parts. Hash each normalized path + LF + exact declaration text including trivia, sorted by path/span and joined with LF. Moves/edits invalidate it. Obtain fingerprints through authorized review, not unapproved helper scripts.

For locations that cannot take an attribute, use a named `#pragma warning disable DRNCV...` with a concrete adjacent comment and a matching restore in the same file. Broad editorconfig, command-line and CI disabling remain organizational review responsibilities. The analyzer cannot guarantee their absence. Suppressions never relax runtime identity, authorization or database safeguards.

## Editing and adoption

DRNCV005 offers a public forwarding constructor only with an accessible exact base constructor and editable partials. Primary/generated/linked ambiguity, conflicting overloads and nontrivial initialization prevent the fix. No Fix All. Workspace changes invalidate the preview. Review before applying.

See [editable assistance](SCAFFOLDING.md) for configuration, repository pairs, combined repository/configuration, selected-property DTO and forwarding-service actions. Aggregate/context and controller actions produce review plans only. Configuration is optional. Its action is a refactoring, never a fabricated warning. Multi-file actions return one solution change, reject stale snapshots and add no project references. The IDE host must support preview and application of that solution change.

Review diagnostics and models when upgrading application projects. Preserve IDs, context/storage identities and authorization. User namespace moves require consumer inventory and model/snapshot evidence. Use standard diagnostic severity settings for reviewed exceptions independently of runtime safeguards. Validate source and package distribution in the intended compiler and IDE hosts.
