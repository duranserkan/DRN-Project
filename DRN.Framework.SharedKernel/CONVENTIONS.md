# SourceKnown application conventions

Application projects receive ownership, placement and boundary warnings by default. Conventions do not configure EF, allocate entity IDs, register services or authorize endpoints. Existing SourceKnown identity, classification and authorization diagnostics remain independent of convention activation.

Provider and composition analysis are bounded. Quiet analysis does not establish model, registration, authorization or performance correctness. Validate those properties through the application's runtime and model checks.

## Terminology

| Term | Meaning |
|---|---|
| **Domain** | Top-level business boundary. Inferred from the project stem or `<DrnDomain>` in `Project` layout, or declared by the first segment under the root namespace and folder in `Folders` layout. |
| **Subdomain** | Business capability or technical area inside a Domain. Declared by the first segment in `Project` layout, the second segment in `Folders` layout, or `[SourceKnownSubdomain]`. |
| **Project stem** | The dot-delimited project-name token immediately preceding the role token, or following it when the role token is first (for example, `Shop` in `Company.Shop.Domain`). |
| **Entity / Role stem** | A type name minus its semantic role suffix (`OrderEntity` -> `Order`, `CategoryDto` -> `Category`, `SalesContext` -> `Sales`). |

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

A single role token can appear anywhere in the name. `Infra`, `Company.Shop.Infra` and `Company.Shop.Infrastructure.Postgres` all have role Infra. No application prefix or shared naming root is required. `DomainHelpers` does not contain a role token. Multiple role tokens, such as `Shop.Domain.Infra`, are ambiguous and report DRNCONVENTIONINPUT001 instead of selecting one. Project and assembly names identify individual projects and assemblies. Standard SDK `RootNamespace` controls namespace placement, not application membership.

Projects with a whole `Framework`, `Test`, `Tests`, `Testing`, `Analyzer`, `Analyzers`, `CodeFixes`, `Generator` or `Generators` token are outside application checks. Unknown names also remain outside these checks. This keeps framework and test-support projects separate without project-specific opt-outs.

Build targets supply convention inputs and basic project metadata for C# projects. The analyzer uses inferred identity to decide which application rules apply. The input fragment's `Version("1")` is a data schema version, independent of project identity. Existing identity, classification and authorization analyzers remain active independently.

Review new warnings when upgrading, particularly with warnings-as-errors. Use standard `.editorconfig` diagnostic severity settings or narrow reasoned suppressions for intentional exceptions. No custom activation or identity property is needed.

Split roles use `DrnConventionRole` items with nonoverlapping `NamespacePrefix` and `LogicalPathPrefix`. Review direct edges against each applicable role, without combining permissions.

Placement uses `Compile`/`Link` logical paths, ordinal case and normalized separators. Physical source paths inside the project are normalized to project-relative paths. Unlinked files under intermediate-output directories do not produce logical-path records, regardless of whether those directories are relative, rooted or parent-relative. They remain compiled and supply semantic evidence. Explicit `Link` metadata is preserved for validation. Absolute logical paths, traversal and external source without a valid link are invalid. Missing paths defer path checks. Organization warnings target handwritten code. Cross-owner checks include inherited signatures and generated uses when a handwritten partial/member or exact ownership input provides an actionable location. Fully generated consumers without such a location remain unreported. Migration identity checks include generated artifacts.

## Ownership and persistence

Concrete application-owned EF `DbContext` subclasses must implement `IDrnContext` when conventions are enabled. An inherited implementation counts. No particular `DrnContext` base is required, and abstract bases receive no duplicate interface-policy warning. A specialized context can use a narrow, justified DRNPERSISTENCE007 exception. External context declarations are not rewritten or certified. Implementing the interface alone does not establish provider setup, model validation, lifecycle hooks or every DRN runtime safeguard.

Business subdomain checks apply to Domain types and entities, persistence contexts/configurations, repository and service bindings, DI-registered Application/Infra services, and SourceKnown controllers. Nested helper types retain their owning type's boundary. Ordinary MVC controllers, Razor page models, tag helpers and unclassified helpers do not acquire business ownership from navigation or utility namespaces. Razor page models may use domain entities; their folders represent navigation. Public API contract checks still apply to MVC controllers, and project dependency and implementation-role checks remain independent. Existing DRNAUTHORIZATION001 enforcement still applies to SourceKnown controllers.

The first namespace segment beneath the role owns the subdomain in single-domain projects (`DrnDomainLayout=Project`, the default). Hosted may first use `Controllers` or `Pages`, and Application may use `Services`. For single-domain projects, the project stem (or explicit `<DrnDomain>`) supplies the domain name. `QA.Domain.Ecommerce.Order.OrderEntity` owns `(QA, Ecommerce)`, `QA.Domain.QA.Order.OrderEntity` owns `(QA, QA)`, and `QA.Domain.Order.OrderEntity` lacks a subdomain. For multi-domain projects, set `<DrnDomainLayout>Folders</DrnDomainLayout>` so the first two segments declare `<Domain>/<Subdomain>`. `Sample.Domain.QA.Common.Order.OrderEntity` and `Sample.Domain.Billing.Common.Invoice.InvoiceEntity` have distinct `(Domain, Subdomain)` owners. Combining `DrnDomainLayout=Folders` with `DrnDomain`, or using a bare `Domain` project without `DrnDomain` or `Folders`, reports DRNCONVENTIONINPUT001.

Domain grouping folders (`<Subdomain>/` in Project layout or `<Domain>/` and `<Domain>/<Subdomain>/` in Folders layout) may contain only folders (DRNSTRUCTURE008). Every Domain project must contain at least one populated subdomain folder (DRNOWNERSHIP004). Only root zero-type files (such as `Usings.cs` and assembly attributes) and application identity declarations (`EntityTypeAttribute<TApp>`, `IAppId`, and `[DomainOnly]` `byte` enums referenced as a parameter of a local `EntityTypeAttribute` subclass) may live at the Domain project root. An optional subdomain-scoped `<Subdomain>/_Shared/` (or `<Domain>/<Subdomain>/_Shared/`) folder is reserved strictly for shared value objects (non-static classes, records, or structs) and may never contain entities, repositories, services, enums, interfaces, delegates, static classes, or persistence types.

Ownership retains the declaring assembly, domain, and subdomain. Bound Infra, Application or Hosted use of a referenced Domain entity can share its `(Domain, Subdomain)` across naming prefixes. Cross-layout references between `Folders`-layout and `Project`-layout projects match when their resolved `(Domain, Subdomain)` pairs agree. Unannotated `Project`-to-`Project` references remain prefix-independent by subdomain. Multi-domain `Project`-layout projects with colliding subdomain names should declare `<DrnDomain>` or use `Folders` layout. Two Domain assemblies remain distinct owners even when their domain and subdomain names match.

Use `[SourceKnownSubdomain("Identity", Reason = "Authentication account ownership")]` (with optional `Domain = "..."` in multi-domain layouts) for technical aliases. The attribute belongs to `DRN.Framework.SharedKernel.Attributes`. It does not waive placement warnings.

Assembly-level `SourceKnownSubdomain(typeof(ExternalType), "Owner", Reason = "...")` identifies one external type. It cannot redefine an established referenced owner.

`SourceKnownPersistence(typeof(OrderEntity))` declares context membership evidence. `Inclusion = SourceKnownPersistenceInclusion.Exclude` declares exclusion. Neither changes EF's model or suppresses DRNOWNERSHIP002. Missing DbSets do not prove exclusion. Attribute-only mappings need no configuration class or suppression.

`SalesReadContext` and `SalesWriteContext` may share OrderEntity within Sales. Context stems differing from their namespace owner need reasoned explicit ownership. Declare isolated destinations where intended. No runtime read-only guarantee or storage change is implied.

## Enums and strict public contracts

Application enums live once in Contract by default, and Domain uses that exact type. Domain-declared enums require `[DomainOnly]` (DRNENUM001). `[DomainOnly]` is invalid outside Domain (DRNENUM002), and `[DomainOnly]` enums cannot appear on public Contract shapes or controller endpoints, including nested or generic wrappers (DRNENUM003). Domain identity `byte` enums may remain CLR-public for attribute constructors.

Contract and Domain enums may define a `0` member only when marked with `[AllowZero]` (DRNENUM004), and `[AllowZero]` requires a named zero member (DRNENUM005). Both rules inspect every resolved member value, including implicit, computed, aliased, later, `[Flags]`, and `[DomainOnly]` zeros. `JsonStringEnumConverter` and default MVC model binding still accept numeric `0` and undefined integer enum values at runtime, so operations must explicitly validate allowed values (for example with allowlist checks or `Enum.IsDefined`).

Endpoint checks include `[ProducesResponseType]` on actions, controllers, and their bases. An action's declarations take precedence over controller declarations for the same HTTP status code.

Public endpoints and Contract messages must not return, accept, inherit from, or nest a `Dto` (DRNDTO005, excluding `[FromServices]` and `[FromKeyedServices]` parameters and inspecting referenced `Application` wrappers and `[ProducesResponseType]` metadata). Public operations returning aggregate data or accepting business payloads use explicit Contract `*Request` and `*Response` types across return types, `[ProducesResponseType]`, and complex `[FromQuery]` parameters (DRNDTO006), while route-only GETs, `StatusCodeResult` (including 204 `NoContentResult`), and untyped `IActionResult`/`ActionResult`/`IResult` need no empty wrappers. `DRNDTO003` verifies `[Personal]` and `[Sensitive]` preservation on both entity `ToDto()` projections and Contract `*Response` object initializers and positional/constructor projections (including nested entity/DTO reads and `abstract` or generic mappers). Apply `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` on strict Request types to reject unexpected caller fields while keeping shared `JsonConventions` compatible with EF JSONB and upstream HTTP payloads.

## Declared inputs

| Item | Required relationship and optional evidence |
|---|---|
| `DrnConventionContext` | Exact context metadata name. Conventional `Repositories` and `Migrations` destinations are inferred from namespace and logical source homes. Declare destinations for alternate or ambiguous layouts, with `Reason` for alternatives. Optional `Domain`, `Subdomain`, `MigrationsAssembly`, `Factory`, `Provider`, `Reason`. Custom construction/provider declarations need a review reason and runtime/model evidence. |
| `DrnConventionOwnership` | Exact type and `Subdomain`. Optional `Domain`, `Kind` (`Entity` or `Value`), and `Reason`. Persistence types cannot become neutral values. |
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

These are directional `ProjectReference` requirements. At least one direct project of each required role satisfies the rule. There is no naming-prefix restriction, ordering requirement or upper limit. Multiple Hosted projects may share Infra, Infra may reference multiple Domain projects, and Domain may reference multiple Contract projects. A transitive reference or package dependency does not satisfy a direct project requirement. Every additional reference still has to obey the allowed direction. DRNARCHITECTURE006 identifies a missing required role. Absence checks require complete, valid reference inventory. Incomplete evidence is not proof of a missing dependency.

Every Infra project must reference Domain projects that collectively contain at least one populated subdomain namespace. For example, a type in `Shop.Domain.Sales` establishes Sales in that declaring assembly. Subdomain evidence is collected across directly referenced Domain projects. Concrete Infra contexts and repositories must have a matching Domain subdomain. Same-named subdomains in different assemblies retain distinct ownership identities. DRNOWNERSHIP003 reports missing Domain subdomain evidence. An empty directory or namespace with no types is insufficient compiler evidence. Missing required references are reported before dependent subdomain checks.

Allowed references remain separate from required references. Domain may reference Contract, Utils and SharedKernel. Contract may reference SharedKernel. Infra may reference Domain, Contract, Utils and SharedKernel. Application may additionally reference Infra. Hosted may additionally reference Application. Utils may reference SharedKernel. SharedKernel has no outward application-layer dependencies.

SharedKernel is a foundation role for shared contracts and abstract entity, DTO and service bases. It does not become an alternative home for concrete application entities, services, persistence implementations or transport. Existing semantic placement and public-contract rules still apply.

## Service registration evidence

DRNDEPENDENCYINJECTION001 checks explicit implementation/service/key demands and eligible services on an observed `AddServicesWithAttributes()` or `AddServicesWithAttributes(typeof(Marker).Assembly)` path. Ordinary Application orchestration and Infra repository implementations need no SourceKnown base or explicit registration item.

The supported implicit boundary is a concrete, closed Application or Infra implementation of a paired `I{ImplementationName}` interface with a recognized role: Application, Domain, Infra, SharedKernel, Utils, Contract or Hosted. Interfaces without a recognized role do not establish a pair. Role recognition does not waive DRNARCHITECTURE003's project-reference direction rules. The interface must declare or inherit instance operations, or inherit a recognized service/repository contract. Required parameters of a single public constructor on these implementations, lifetime-attributed services and MVC/Razor transports also establish service relationships when exactly one eligible implementation is available. This includes concrete self-services and literal string `[FromKeyedServices]` selectors. Optional parameters and ambiguous constructor or implementation choices do not establish exact demands. Entities, declared neutral values, DTOs, EF configurations, contexts and configuration-bound types are excluded from implicit implementation discovery.

A paired interface without an observed key demand needs a matching registration under any supported key. Explicit demands and observed constructor dependencies require their exact key or unkeyed registration. A keyed empty string differs from unkeyed registration; the optional project-item `Key` is unkeyed when omitted or empty. Effective inherited lifetime attributes count according to DRN scan behavior. Supported manual registrations are direct generic `AddScoped`, `AddTransient`, `AddSingleton` and their keyed counterparts with implementation types, including concrete self-registration. Factory delegates, non-string keys, custom modules/helpers and unobserved composition are not absence proofs. Unknown composition defers the warning.

Names alone, data-only interfaces, unpaired unused interfaces and arbitrary helper classes do not establish service intent. Such cases need explicit demands or runtime composition review. The analyzer cannot infer every intended service. Use a narrow justified suppression when registration is intentionally unnecessary, and do not add duplicate manual registrations for attributed services.

Eligible `HostedServiceAttribute` implementations satisfy unkeyed `IHostedService` demands and DRNDEPENDENCYINJECTION002's matching lifetime requirement for an `IHostedService` pair. This includes implementations named `HostedService` and descendants of `BackgroundService`. Keyed demands remain separate. Unrelated external calls such as `Math.Abs` beside a scan do not make composition unknown.

Custom nongeneric lifetime bindings can be proven through source-visible explicit constructor chains forwarding `typeof(...)` or type-valued attribute arguments to `LifetimeAttribute`'s service type parameter. Arbitrary expressions and unavailable bodies remain unproven. DRNDEPENDENCYINJECTION001 still defers custom key semantics.

When DRNDEPENDENCYINJECTION002 reports a binding failure, DRNDEPENDENCYINJECTION001 does not repeat it for the same implementation/interface pair. DRNDEPENDENCYINJECTION001 still checks other service demands and key mismatches after a valid binding. Neither rule proves that an attribute scan is called at runtime; DRNDEPENDENCYINJECTION001 defers when no supported scan is observed.

DRNDEPENDENCYINJECTION003 requires Hosted, Application, Infra and Utils projects to declare a public top-level nongeneric static `*Module` class. A public static nongeneric method must accept and return `IServiceCollection` and directly call the DRN attribute scan on that parameter for its own assembly. Parameterless scans and explicit `typeof(LocalType).Assembly` scans count. Nested callbacks and scans of other assemblies do not count. Domain, Contract, SharedKernel, framework and test projects are excluded. This checks the module's structure, not whether the host invokes it.

DRNDEPENDENCYINJECTION002 independently requires one lifetime attribute bound to the matching interface on concrete, closed Application/Infra service implementations paired by exact interface name (`AnswerRepository : IAnswerRepository`). Generic and keyed lifetime derivatives count, including custom derivatives. Direct attributes take precedence over inherited attributes. The binding must match the exact interface symbol and generic arguments; self-registration, other service bindings and multiple selected lifetime attributes fail the rule. Use `[Scoped<IAnswerRepository>]`, `[Transient<IAnswerRepository>]` or `[Singleton<IAnswerRepository>]` according to dependency lifetime requirements. Manual registration and unknown composition do not waive this binding rule. Existing service eligibility exclusions keep entities, DTOs, neutral values, contexts, configurations, bound options, controllers and page models outside its scope. Exact justified convention exceptions remain available.

## Diagnostic catalog

The [full diagnostic reference](CONVENTION-DIAGNOSTICS.md) provides exact messages, applicability, examples, remedies, exceptions and fix risks for every rule.

Convention rules default to Warning in enabled application projects, affecting warning-as-error consumers. No fix renames contexts, moves configurations or rewrites migrations.

| ID | Why and ordinary remedy | Valid / invalid example | Exception and limit |
|---|---|---|---|
| DRNARCHITECTURE001 | Review incomplete public contract inspection. | Public model fits the analysis budget / a member's nested model exhausts it. | Review the named member or reduce its public shape. |
| DRNDTO001 | Each concrete, closed Domain SourceKnownEntity overrides public `ToDto()` returning its Contract DTO. | `public class CategoryEntity : AggregateRoot` with `public override CategoryDto ToDto() => new(this) { Name = Name };` | Metadata-only and partial projections are supported. |
| DRNDTO002 | Put the DTO in its subdomain and plural entity folder. | QA/Categories/CategoryDto.cs | DRNSTRUCTURE001 checks logical source placement. |
| DRNDTO003 | Preserve each mapped Personal or Sensitive property classification on DTO and Contract `*Response` properties. | A renamed personal property still requires Personal. | Omitted fields need no destination member. |
| DRNDTO004 | Place public entity-prefixed requests and responses beside their DTO. | QA/Categories/CategoryPostRequest.cs | The longest matching entity stem in the Contract subdomain wins. |
| DRNDTO005 | Keep `Dto` types out of public Contract messages and controller endpoints. | `CategoryResponse` with explicit fields / `OrderResponse : OrderDto` or endpoint returning `OrderDto`. | Honors `[NonAction]` and `[NonController]`. |
| DRNDTO006 | Use explicit Contract `*Request` and `*Response` types on public endpoints. | `TagController` returning `TagResponse` / endpoint returning raw Contract model. | Route-only GET, `NoContentResult` (204), scalars, and pagination types are exempt. |
| DRNENUM001 | Define application enums in Contract or mark internal Domain enums `[DomainOnly]`. | `TagType` in Contract, `[DomainOnly] SampleEntityTypes` in Domain / unmarked Domain enum. | Single definition per enum vocabulary. |
| DRNENUM002 | Restrict `[DomainOnly]` to Domain enums. | Remove `[DomainOnly]` when moving an enum to Contract. | Prevents public Contract enums from carrying `[DomainOnly]`. |
| DRNENUM003 | Keep `[DomainOnly]` enums off public Contract shapes and controller endpoints. | `SampleEntityTypeAttribute` constructor in Domain / `List<SampleEntityTypes?>` on a Response. | Bounded public-shape graph walk. |
| DRNENUM004 | Require `[AllowZero]` on any Contract or Domain enum with a zero member. | `[AllowZero] TagType { System = 0, User }` / implicit, computed, aliased, later, or `[Flags]` zero without `[AllowZero]`. | Compile-time enum convention. Operations validate runtime input. |
| DRNENUM005 | Require a named zero member whenever `[AllowZero]` is applied. | Remove `[AllowZero]` or declare an explicit `= 0` member. | Prevents misleading opt-ins. |
| DRNOWNERSHIP004 | Ensure every Domain project contains at least one populated subdomain folder. | Domain project with `QA/Tag/TagEntity.cs` / Domain project with only root files. | Project-level exception target `<AssemblyName>:Domain`. |
| DRNSTRUCTURE008 | Keep Domain grouping folders folders-only and `_Shared` subdomain-scoped for value objects. | `QA/Tag/TagEntity.cs`, `QA/_Shared/Money.cs`, root `Usings.cs` and `SampleEntityTypes.cs` / file directly under `QA/`. | Generated files are ignored; partial declarations are checked per file. |
| DRNARCHITECTURE002 | Keep external contracts free of persistence objects. | Dto/Guid / public collection containing an entity. | Approved neutral value model. Dto mapping constructors may accept their source entity. Complex or external graphs remain bounded. |
| DRNARCHITECTURE003 | Respect direct application-role edges. | Application to Infra / Domain to Infra. | Exact reasoned integration edge. Direct provenance and resolved assembly/project identity are required. Package IDs do not establish roles. |
| DRNARCHITECTURE004 | Place the implementation in its owning layer. | Context in Infra, controller in Hosted / DTO declared in Domain. | Exact reasoned role exception. |
| DRNARCHITECTURE005 | Remove a forbidden layer dependency. | Domain uses an EF mapping attribute / Domain uses DbContext or EntityTypeBuilder. | Reports independently of implementation placement. |
| DRNARCHITECTURE006 | Supply at least one direct project reference per required role. | Hosted directly references Application and shared Infra / Infra available only transitively. | No application-root restriction. Complete valid reference inventory is required for absence claims. |
| DRNCONVENTIONEXCEPTION001 | Keep observable exceptions exact and reviewable. | Exact target with reason / wildcard, blank reason or due review. | Standard channels below. Source fingerprints bind reviewed content. External switches cannot be universally audited. |
| DRNCONVENTIONINPUT001 | Supply unambiguous project identity and consistent facts. | `Shop.Infrastructure.Sql` / `Shop.Domain.Infra`, conflicting owners or invalid logical path. | Unknown names do not activate application checks. Missing evidence is not guessed. |
| DRNDEPENDENCYINJECTION001 | Establish effective registration for explicit or inferred service relationships. | Matching supported registration / eligible service on an observed scan without matching lifetime binding. | Reasoned custom registration. Eligibility and unknown composition limits are described above. |
| DRNOWNERSHIP001 | Give relevant entities and bound services a clear owner. | `Domain.Sales.Order.OrderEntity` / unowned `Domain.OrderEntity`. | Reasoned exact owner, including technical Identity. Abstract/open intermediates are excluded. |
| DRNOWNERSHIP002 | Make intentional cross-subdomain entity coupling visible. | Same-owner CQRS membership / Sales code uses an Inventory entity without a narrow exception. | Use owning contracts or identifiers, correct misplaced ownership/code, or justify the exact reuse. Membership alone is not an exception. |
| DRNOWNERSHIP003 | Establish Domain subdomains for Infra. | Domain has Sales types and Infra Sales contexts/repositories / empty Domain or unmatched Infra owner. | Types in child namespaces establish subdomains. Empty folders do not. |
| DRNPERSISTENCE001 | Provide the options constructor for a supported construction path. | Public `DbContextOptions<SalesContext>` constructor / DRN context with only a parameterless constructor. | Reasoned custom factory. Existing equivalent generic constraint errors are not duplicated. Guarded additive fix only. |
| DRNPERSISTENCE002 | Preserve Npgsql on the selected construction path. | Forwarded convention options / direct DRN options constructor discards them for null or fresh empty options. | Custom OnConfiguring/factory/helper paths remain unknown. Empty local options delegates do not prove absence. |
| DRNPERSISTENCE003 | Retain required base model setup. | `base.OnModelCreating(model)` / empty override. | Reasoned replacement with integration evidence. Current proof covers straight-line bodies and bounded local forwarding. Arbitrary flow/helpers remain unknown. |
| DRNPERSISTENCE004 | Resolve contradictory persistence declarations. | One explicit inclusion / simultaneous include and exclude. | Shared/external membership is valid. No complete EF model inference or missing-DbSet warning. |
| DRNPERSISTENCE005 | Make intended configurations discoverable. | Same assembly, exact/child context namespace / declared sibling configuration with a proven default scan. | Explicit associations and unambiguous context-area/membership evidence count. Ambiguous CQRS associations need selection. Attribute-only code is valid. |
| DRNPERSISTENCE006 | Preserve declared migration/context association. | Matching context/assembly/destination / known artifact in conflicting destination. | Explicit separate migration assembly. No EF schema equivalence claim or historical rewrite. |
| DRNPERSISTENCE007 | Require IDrnContext on concrete application persistence contexts. | Inherited interface implementation / concrete DbContext without IDrnContext. | Narrow specialized-context exception. Abstract bases are excluded. No runtime-safeguard certification. |
| DRNPERSISTENCE008 | Expose aggregate roots through DrnContext DbSet properties. | DbSet of an AggregateRoot descendant / DbSet of a child entity or keyless row. | Includes inherited instance properties. Unresolved generic entities are deferred. Plain DbContext and DrnContextIdentity are outside this rule. |
| DRNREPOSITORY001 | Keep repository binding compatible with persistence. | Repository uses included OrderEntity / repository targets explicitly excluded OrderEntity. | Exact intentional exception with model evidence. Current reporting focuses on explicit exclusion and supported Ignore evidence. |
| DRNREPOSITORY002 | Keep service and repository aggregate bindings consistent. | OrderService returns OrderRepository / resolved incompatible repository expression. | Exact reviewed adaptation. Current proof is bounded to supported repository expressions. |
| DRNSTRUCTURE001 | Align namespace and logical source home. | `Sales/Order/OrderEntity.cs` under `Domain.Sales.Order` / divergent partial home. | Exact placement exception or split-role mapping. No automatic move. |
| DRNSTRUCTURE002 | Make context names recognizable without changing ownership. | `SalesReadContext` / `Store` or `Context`. | Narrow legacy operational exception. Plain DbContext subclasses count; unrelated name lookalikes do not. |
| DRNSTRUCTURE003 | Infer conventional repository and migration destinations. Require explicit destinations for alternate or ambiguous layouts. Validate explicit destinations even without artifacts. | `Sales/Repositories`, `Sales/Migrations` without metadata / conflicting logical homes without a declaration. | Reasoned alternate output. No directory scanning or fake initial migration. |

## Aggregate names and repository pairs

Concrete aggregates use an `Entity` suffix and a matching final namespace and logical directory segment: `OrderEntity` belongs in `QA/Order/OrderEntity.cs` under `<DomainRoot>.QA.Order`. Partial declarations follow the same directory rule. Abstract/open generic helpers are not concrete aggregates. Repository classes and interfaces end in `Repository`, semantic application services and their contracts end in `Service`, and MVC controllers end in `Controller`. Each suffix needs a nonempty stem. Existing context naming remains governed by DRNSTRUCTURE002. Other role folders retain their existing conventions.

Each concrete Domain aggregate requires an interface derived from `ISourceKnownRepository<TEntity>` in the same Domain assembly, namespace and logical folder. For example, `OrderEntity.cs` and `IOrderRepository.cs` both belong in `QA/Order`. Infra must provide a concrete `SourceKnownRepository<TContext,TEntity>` implementing that interface for aggregates assigned to its contexts. Assignment evidence includes DbSets, explicit persistence inclusion and supported configuration application/default discovery. Explicit exclusion or Ignore removes the obligation for that context. An aggregate used by multiple contexts needs one implementation in that Infra project. Merely referencing a Domain assembly does not require repositories for all its aggregates.

| Diagnostic | Requirement |
|---|---|
| DRNDEPENDENCYINJECTION002 | Register each concrete, closed Application/Infra service implementing an exact I{Name} interface through one matching LifetimeAttribute. |
| DRNDEPENDENCYINJECTION003 | Expose a public static *Module class in Hosted, Application, Infra and Utils projects with an IServiceCollection method calling AddServicesWithAttributes() for its assembly. |
| DRNPERSISTENCE009 | Directly apply a custom NpgsqlDbContextOptionsAttribute descendant to each concrete, closed DRN context. Inherited options alone do not satisfy this requirement. |
| DRNREPOSITORY003 | Declare the aggregate's repository interface in the same Domain namespace and logical folder. |
| DRNREPOSITORY004 | Implement the assigned aggregate's Domain repository interface through SourceKnownRepository in Infra. |
| DRNSTRUCTURE004 | Use a nonempty Entity, Repository, Service, Controller or Config name for the corresponding semantic role. |
| DRNSTRUCTURE005 | Match the aggregate namespace and directory to its name without Entity. |
| DRNSTRUCTURE006 | Put Infra repository implementations in Repositories, configurations in Configurations, and DRN migrations/snapshots in Migrations. |
| DRNSTRUCTURE007 | Start custom options names with the applied context name and end with Options, before an optional Attribute suffix. |

Dedicated folders are the immediate logical parent and final namespace segment. For example, `QA/Configurations/OrderConfig.cs`, `QA/Repositories/OrderRepository.cs` and `QA/Migrations/QAContextModelSnapshot.cs`. Domain interfaces remain beside their aggregates. Compile/Link paths govern placement. Generated migrations are checked through their context association. A reasoned destination override does not itself waive DRNSTRUCTURE006. Custom Npgsql options attributes applied to a DRN context follow `QAContext...Options[Attribute]`. Shared framework defaults are exempt. Options naming does not add an Options folder requirement.

Concrete, closed DRN contexts must directly declare custom options. Prefer `QAContextNpgsqlDbContextOptionsAttribute`, applied as `[QAContextNpgsqlDbContextOptions]`. `NpgsqlDbContext` is recommended within the name. Shared framework defaults are exempt from naming checks but do not replace this direct declaration.

## Narrow exceptions

Correct real violations first. Standard suppression covers the named diagnostic on one symbol. Use pair-specific project evidence to exempt only one consumer/entity relationship.

```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "DRN.Conventions.Ownership", "DRNOWNERSHIP002",
    Justification = "This reconciliation reads the existing inventory aggregate inside the reviewed transaction boundary.")]
public void ReconcileInventory() { }
```

This illustrates suppression placement, not an operation template. Project exceptions use resolvable documentation-comment targets or project-level target tokens:

- `DRNARCHITECTURE006`: `SymbolTarget="<AssemblyName>:Application"`, `<AssemblyName>:Infra`, `<AssemblyName>:Domain`, or `<AssemblyName>:Contract` (for example, `Shop.Domain:Contract`).
- `DRNOWNERSHIP003`: `SymbolTarget="<AssemblyName>:Domain"` when no populated Domain subdomain exists, or the unresolved context/repository `T:...` symbol target.
- `DRNDEPENDENCYINJECTION003`: `SymbolTarget="<AssemblyName>:Module"`.
- `DRNREPOSITORY004`: optionally qualify with `Context="Shop.Infra.Sales.SalesContext"` alongside `SymbolTarget` and `Entity`.

```xml
<DrnConventionException Include="DRNOWNERSHIP002"
                        SymbolTarget="M:Shop.Application.Sales.InventoryReconciler.ReconcileInventory"
                        Entity="Shop.Domain.Inventory.Stock"
                        Reason="Reviewed cross-domain reconciliation in one transaction."
                        Owner="Sales team"
                        Review="2027-01-15" />
```

`Review` must be later than today's UTC date. Due reviews refuse the waiver with DRNCONVENTIONEXCEPTION001. No wildcards or unresolved targets. Optional lowercase SHA-256 `Fingerprint` requires valid logical paths for all source parts. Hash each normalized path + LF + exact declaration text including trivia, sorted by path/span and joined with LF. Moves/edits invalidate it. Obtain fingerprints through authorized review, not unapproved helper scripts.

For locations that cannot take an attribute, use a named `#pragma warning disable DRNOWNERSHIP002` with a concrete adjacent comment and a matching restore in the same file. Broad editorconfig, command-line and CI disabling remain organizational review responsibilities. The analyzer cannot guarantee their absence. Suppressions never relax runtime identity, authorization or database safeguards.

## Editing and adoption

DRNPERSISTENCE001 offers a public forwarding constructor only with an accessible exact base constructor and editable partials. Primary/generated/linked ambiguity, conflicting overloads and nontrivial initialization prevent the fix. No Fix All. Workspace changes invalidate the preview. Review before applying.

See [editable assistance](SCAFFOLDING.md) for configuration, repository pairs, combined repository/configuration, selected-property DTO and forwarding-service actions. Aggregate/context and controller actions produce review plans only. Configuration is optional. Its action is a refactoring, never a fabricated warning. Multi-file actions return one solution change, reject stale snapshots and add no project references. The IDE host must support preview and application of that solution change.

Review diagnostics and models when upgrading application projects. Preserve IDs, context/storage identities and authorization. User namespace moves require consumer inventory and model/snapshot evidence. Use standard diagnostic severity settings for reviewed exceptions independently of runtime safeguards. Validate source and package distribution in the intended compiler and IDE hosts.
