# DRN diagnostic reference

Each prefix identifies the requirement that needs attention. Use the full ID for a per-rule severity setting. The Roslyn category controls category-wide settings and can span several prefixes.

```ini
[*.cs]
dotnet_diagnostic.DRNARCHITECTURE005.severity = error
dotnet_analyzer_diagnostic.category-DRN.Conventions.Architecture.severity = error
```

The rules from Architecture through Convention exceptions default to Warning in recognized application projects. Their messages below show placeholders for the relevant symbols or inputs. DRNPERSISTENCE001 offers an individual constructor code fix. See [terminology](CONVENTIONS.md#terminology), [project identity and exceptions](CONVENTIONS.md), and [editable assistance](SCAFFOLDING.md).

## Entity DTOs and public contracts

| ID | Category | Requirement |
|---|---|---|
| DRNDTO001 | DRN.Conventions.Contract | A concrete, closed Domain `SourceKnownEntity` overrides public parameterless instance `ToDto()` returning its matching public Contract DTO. Return the DTO directly with `new(this)` or the equivalent explicit constructor. |
| DRNDTO002 | DRN.Conventions.Structure | Place the DTO under the owning `(Domain, Subdomain)` and plural entity folder, for example `QA/Categories/CategoryDto.cs`. DRNSTRUCTURE001 checks the logical path against the namespace. |
| DRNDTO003 | DRN.Conventions.Contract | Preserve each positive Personal and Sensitive classification on the corresponding DTO or Contract `*Response` property. Checks object initializers, record `with`-expressions, positional/constructor arguments matched to destination properties, nested entity/DTO property chains (such as `entity.Model.StringValue`, `dto.Contact.Email`, and LINQ extension chains such as `entity.Contacts.First().Email`), and projections inside `abstract`, generic, and nested-in-generic types across `Application`, `Contract`, `Domain`, `Hosted`, and `Infra`. Unassigned DTO properties use the same-name entity property when one exists. |
| DRNDTO004 | DRN.Conventions.Structure | Place a public entity-prefixed `Request` or `Response` beside its DTO in the same namespace and logical compile folder. |
| DRNDTO005 | DRN.Conventions.Contract | Do not return, accept, inherit from, or nest a `Dto` in public Contract messages or controller endpoints. Checks inherited bases (including closed-generic, non-Contract, and referenced bases), implemented generic interfaces, generic containing types, referenced `Application`, `Contract`, `Domain`, `Hosted`, and `Infra` wrappers, write-only public properties, and `[ProducesResponseType]` metadata on untyped `IActionResult`/`ActionResult`/`IResult` actions. Honors `[NonAction]` and `[NonController]`, and excludes `[FromServices]` and `[FromKeyedServices]` parameters. Bounded to static signatures and metadata. See [DRNARCHITECTURE001](#drnarchitecture001-review-incomplete-public-contract-inspection) for the shared traversal limit. |
| DRNDTO006 | DRN.Conventions.Contract | Use explicit Contract `*Request` and `*Response` types on public endpoints across return types (including standard collection wrappers such as `IAsyncEnumerable<T>` and `ImmutableArray<T>`), `[ProducesResponseType]` metadata, and request parameters (including complex `[FromQuery]` models and inferred JSON bodies on `[ApiController]` actions). Parameters injected with `[FromServices]` or `[FromKeyedServices]` are exempt. On ordinary `ControllerBase` controllers, `Contract`, `Application`, `object`, and `dynamic` payloads require explicit Contract messages while `Hosted`-local operational models (such as `ClientErrorPayload`) remain exempt. Route-only GETs, status-only `StatusCodeResult` subclasses (such as `OkResult` and 204 `NoContentResult`), untyped `IActionResult`/`ActionResult`/`IResult`, allowlisted scalar types, files, `ProblemDetails`, and pagination models are exempt. |

To escalate `DRNDTO001` (or any convention diagnostic) to a build error in projects without `TreatWarningsAsErrors`, add an `.editorconfig` entry:

```ini
[*.cs]
dotnet_diagnostic.DRNDTO001.severity = error
```

DRNDTO004 matches the declared type name against public entity DTOs in the same Contract project and subdomain. The longest prefix at a PascalCase word boundary determines the owner. With only `ProductDto`, `ProductBatchRequest` belongs in `Products/`. Adding `ProductBatchDto` makes `ProductBatches/` its destination. `OrderDto` matches `OrderResponse` and leaves `OrderingRequest` unmatched. A request or response can define its own payload.

These conventions default to Warning. A projection can expose only the inherited DTO metadata. Domain members can be omitted. A name-rule exemption such as `[Sensitive(false)]` does not preserve a positive classification.

A partial mapper can supply its body in the implementing declaration. The DTO constructor can have additional optional parameters when the mapper calls `new(this)`. The DTO, its containing types and all their type arguments must be public. For an array argument, this includes its element type.

DRNDTO003 checks property reads in dictionary keys and values against the enclosing DTO property. In `Extra = { [Email] = Secret }`, a personal email and a sensitive secret require both annotations on `Extra`. For `Display = Format(new Options { Prefix = Name })`, the classification of `Name` belongs on `Display`. The analyzer checks inputs visible at the call site. Review any additional data read inside the helper body.

In `Contacts = new() { new ContactDto { Label = Email } }`, a personal `Email` requires `[Personal]` on `ContactDto.Label`. In a scalar list initialized with `{ Email }`, the collection property carries the classification. An expression such as `nameof(Email)` exposes only the property name.

The same rules apply to bracket collection expressions. If a custom `Add` extracts the label and stores it as a scalar, classify the collection property. Visible inputs to a collection constructor or a user-defined conversion, including conversions in `??` and spread expressions, contribute to the classification of the property receiving the result.

DTO folders use a deterministic suffix. A consonant followed by lowercase `y` becomes `ies`. Stems ending in lowercase `s`, `x`, `z`, `ch` or `sh` take `es`. All other stems take `s`, including `Person` → `Persons`. The IDE action requires the result to be a valid namespace segment.

For DRNDTO003, copy each positive source property annotation to the mapped DTO property, including a nested destination such as `Contact.Email`. An inherited annotation on an override counts. When the concrete receiver is known, annotations on its selected virtual override or interface implementation also count, including through `?.`. A class annotation alone cannot satisfy a source property annotation. When protection comes solely from a source type, an equivalent destination type or property classification preserves it. Classification checks on the initializer remain active whether DRNDTO001 reports a problem or an approved exception covers the DTO's name, namespace, accessibility or constructor arguments.

| ID | Message template | Exception target |
|---|---|---|
| DRNDTO001 | `'{0}' must override public ToDto() returning {1} from its Contract subdomain and constructing it with this.` | Entity type |
| DRNDTO002 | `Place '{0}' under its subdomain in the '{1}' folder and matching namespace.` | DTO type |
| DRNDTO003 | `Projected property '{0}' requires [{1}] from mapped property '{2}'.` | Entity or projecting type |
| DRNDTO004 | `Place '{0}' in '{1}' with namespace '{2}' alongside its entity DTO.` | Request or response type |
| DRNDTO005 | `Public contract or endpoint '{0}' exposes DTO type '{1}'; use an explicit Contract Request or Response without inheriting or nesting a DTO.` | Public member, message type, or controller |
| DRNDTO006 | `Public endpoint '{0}' must use an explicit Contract {1} type instead of '{2}'.` | Action method or controller |

For inherited application actions, DRNDTO006 reports on the concrete controller and names each affected action. Inherited standard SourceKnown actions that share an invalid request or response type produce one warning per type and direction on that controller.

## Enum conventions

Roslyn category: `DRN.Conventions.Contract`. All five rules default to Warning in recognized application projects.

| ID | Message template | Requirement |
|---|---|---|
| DRNENUM001 | `Enum '{0}' is declared in Domain without [DomainOnly]. Move it to Contract for shared vocabularies or mark it [DomainOnly].` | Declare shared application enums once in Contract. Mark internal Domain enums with `[DomainOnly]`. |
| DRNENUM002 | `Enum '{0}' cannot carry [DomainOnly] outside a Domain project; remove [DomainOnly] from public Contract vocabularies.` | Remove `[DomainOnly]` from any enum outside Domain. |
| DRNENUM003 | `Public contract or endpoint '{0}' exposes [DomainOnly] enum '{1}'; move the enum to Contract and remove [DomainOnly] before exposing it.` | Rejects `[DomainOnly]` enums anywhere in public Contract or endpoint graphs, including nested models, generic wrappers (such as `List<SampleEntityTypes?>`), closed generic bases and implemented interfaces, generic containing types, arrays, `ValueTuple`s, referenced `Application`, `Contract`, `Domain`, `Hosted`, and `Infra` wrappers, `[ProducesResponseType]` metadata, and write-only properties. Excludes `[FromServices]` and `[FromKeyedServices]` parameters and valid `Dto` mapping constructors. Bounded to static signatures and metadata. See [DRNARCHITECTURE001](#drnarchitecture001-review-incomplete-public-contract-inspection) for the shared traversal limit. |
| DRNENUM004 | `Enum '{0}' defines zero member '{1}' without [AllowZero]. Start members at a nonzero value or mark the enum with [AllowZero].` | Checks all resolved zero values across Contract and Domain enums, including implicit, computed, aliased, later, `[Flags]`, and `[DomainOnly]` zeros. |
| DRNENUM005 | `Enum '{0}' is marked with [AllowZero] but defines no named member with value 0. Remove [AllowZero] or define a named zero member.` | Rejects `[AllowZero]` when no named member resolves to `0`. |

## Entity identity

Roslyn category: `DRN.Domain`. For examples of entity declarations, partitions, repository type arguments and metadata calls, see the [entity guide](README.md#compile-time-roslyn-analyzers).

| ID | Default | Requirement |
|---|---|---|
| DRNENTITY001 | Error | Declare an entity type attribute on each eligible concrete entity. |
| DRNENTITY002 | Error | Give each entity a unique type value within its application partition. |
| DRNENTITY003 | Error | Apply entity type attributes only to eligible concrete entities. |
| DRNENTITY004 | Warning | Give each entity a unique class name within its partition. |
| DRNENTITY005 | Error | Use a single application partition unless the compilation permits multiple partitions. |
| DRNENTITY006 | Error | Publish a constant partition ID and return it from the application ID getter. |
| DRNENTITY007 | Error | Keep the declared application partition in the range 0 through 127. |
| DRNENTITY008 | Error | Apply a derived entity attribute that forwards one byte-backed enum value unchanged to its base constructor. |
| DRNENTITY009 | Error | Use a supported constant partition value in an entity identity expression. |
| DRNENTITY010 | Error | Use a defined constant entity ID format. |
| DRNENTITY011 | Warning | Preserve inherited entity identity members. |
| DRNENTITY012 | Warning | Declare separate concrete entity types for distinct identities in a generic family. |
| DRNENTITY013 | Error | Use a concrete entity type in metadata operations, explicit aliases and repository bindings. |
| DRNENTITY014 | Error | Initialize entity ID operations before calling them on a fresh entity. |

## Data classification

Roslyn category: `DRN.Classification`. See [classification examples, code fixes and runtime boundaries](https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Utils/Logging/Diagnostics/README.md#compile-time-classification).

| ID | Default | Requirement |
|---|---|---|
| DRNCLASSIFICATION001 | Error | Classify matching model properties or declare a reviewed false positive. |
| DRNCLASSIFICATION002 | Error | Apply name exemptions to eligible properties and correct invalid attribute forwarding. |
| DRNCLASSIFICATION003 | Error | Protect classified capture paths or acknowledge an eligible exact-name match. |
| DRNCLASSIFICATION004 | Error | Use a supported classification attribute. |
| DRNCLASSIFICATION005 | Error | Move matching anonymous properties to a named, annotated model. |
| DRNCLASSIFICATION006 | Error | Classify a dictionary key or acknowledge an eligible exact-name match. |
| DRNCLASSIFICATION007 | Error | Supply a readable, valid, unambiguous classification catalog. |
| DRNCLASSIFICATION008 | Error | Remove or redact sensitive logging values. |
| DRNCLASSIFICATION009 | Error | Put the exact name and a nonempty justification in the exemption comment immediately above the logging statement. |
| DRNCLASSIFICATION010 | Warning | Remove or correct an unused name exemption comment. |
| DRNCLASSIFICATION011 | Error | Reduce the captured model or classify sensitive subtrees when model traversal reaches its work limit. |
| DRNCLASSIFICATION012 | Error | Remove or redact the capture, or classify its property or type directly, when interface classification reaches its 256-property limit. |

## Controller authorization

**DRNAUTHORIZATION001** is an Error in `DRN.Security`. Declare a nonblank named policy on the SourceKnown controller or exposed action. The message identifies a missing policy or an anonymous-access declaration that bypasses authorization. See [Source-Known controllers](https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Hosting/README.md#source-known-controllers).

## Hosting

Both diagnostics are Errors in the Roslyn category `Hosting`. See the [Hosting guide](https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Hosting/README.md#automatic-hosted-accessors).

| ID | Requirement |
|---|---|
| DRNHOSTING001 | Correct the hosted accessor declaration, input or name collision identified by the generator. |
| DRNHOSTING002 | Give every class derived from `DrnProgramBase<TProgram>` a name ending in `Program`. |

## Architecture

### DRNARCHITECTURE001: Review incomplete public contract inspection

Roslyn category: `DRN.Conventions.Configuration`.

> Inspection of public contract member '{0}' reached the analysis limit before verifying that its public model is free of persistence and domain types.

Review the named member's public signature and nested model. A smaller Request or Response model can make the contract fully inspectable. `DRNARCHITECTURE001` is reported once per member when persistence (`DRNARCHITECTURE002`), DTO (`DRNDTO005`), or `[DomainOnly]` enum (`DRNENUM003`) public-shape traversal exceeds 4,096 nodes. A contract's base types, traversed interfaces (including internal interfaces), and the public types they expose share this limit.

### DRNARCHITECTURE002: Keep external contracts free of persistence types

Roslyn category: `DRN.Conventions.Contract`.

> Public contract member '{0}' exposes persistence/domain type '{1}'; expose an explicit Contract Request/Response or value model.

Public Contract and MVC API shapes should expose explicit Contract Request/Response or value models rather than entities, contexts or repository objects. Razor page models are presentation models and are excluded from this API-shape check. Dto Guid/timestamp mapping is valid. A nested array/tuple/generic wrapper containing Order is invalid. An unrelated value class named Entity is not a persistence type.

Public `DbSet<T>` properties expose persistence even when `T` is an ordinary class.

An exact Dto constructor forwarding its source entity is allowed. Private nonserialized framework state is not external API. Approved neutral values and referenced `Application`, `Contract`, `Domain`, `Hosted`, and `Infra` wrapper types remain eligible for public-shape inspection rather than hiding nested entities or persistence types. Models exceeding the analysis limit report incomplete evidence and require review. Object/dynamic/custom serialization requires application review. Any DTO/API rewrite needs explicit compatibility and field decisions.

### DRNARCHITECTURE003: Remove a forbidden direct reference

Roslyn category: `DRN.Conventions.Architecture`.

> Direct {0} reference from '{1}' to '{2}' violates the declared role matrix; remove it or document the exact exception.

Direct provenance distinguishes declared dependencies from transitive compiler availability. Application to Infra is valid. Domain to Infra is not. All application roles may depend inward on SharedKernel, which cannot depend outward on application layers. Roles come from exported project names or, when absent, actual resolved assembly names. Package IDs do not establish project roles. Exact known framework packages retain their documented restrictions. See the [dependency rules](CONVENTIONS.md#progressive-dependency-rules).

Remove the actual direct edge or justify its exact project/package target. Split roles do not gain a union of privileges. Missing direct-reference provenance remains incomplete evidence even when the resolved assembly has an inferred role. No fix changes package/project references.

### DRNARCHITECTURE004: Place the implementation in its owning layer

Roslyn category: `DRN.Conventions.Architecture`.

> Implementation '{0}' ({1}) belongs to role '{2}'.

Contexts, configurations and repository implementations belong in Infra, entities and repository contracts in Domain, DTOs in Contract, services in Application and transport in Hosted.

SharedKernel may contain shared contracts and abstract entity, DTO and service bases. Concrete application entities and service implementations still belong in their owning layers. SharedKernel is not a persistence or transport layer.

Correct the role/placement or justify an exact exceptional implementation. Utils cannot hide application implementation types. A SourceKnown controller composes `ISourceKnownService<TEntity>` and remains transport even when it also implements a service interface. Semantic lookalikes are excluded. Moves are not automated because assembly discovery and public contracts can change.

### DRNARCHITECTURE005: Remove a forbidden layer dependency

Roslyn category: `DRN.Conventions.Architecture`.

> Type '{0}' in layer '{1}' uses {2} APIs. Remove this dependency.

A Domain class that uses `DbContext` or `ModelBuilder` must remove that persistence dependency. Lightweight EF mapping attributes such as `Owned` remain valid. Repeated uses of the same dependency family appear as evidence on one finding per type. A misplaced DTO can also report DRNARCHITECTURE004. Moving it into Contract resolves placement, while any forbidden persistence dependency still requires a separate fix.

### DRNARCHITECTURE006: Add a required direct reference

Roslyn category: `DRN.Conventions.Architecture`.

> Project '{0}' requires a direct project reference to its '{1}' layer.

Hosted projects require at least one direct Application and one direct Infra project reference. Infra requires at least one Domain. Domain requires at least one Contract. These requirements apply whenever the corresponding layer exists, even before its code uses the dependency. Naming prefixes do not constrain references. Multiple Hosted projects may share Infra, and multiple Domain or Contract references are valid. Packages and transitive references do not satisfy a direct project requirement. Every direct reference must still obey DRNARCHITECTURE003's allowed direction.

Add the missing `ProjectReference` or correct a misidentified project name. Intentional project exceptions use `SymbolTarget="<AssemblyName>:Application"`, `<AssemblyName>:Infra`, `<AssemblyName>:Domain`, or `<AssemblyName>:Contract`. The analyzer does not create projects or references. Incomplete or invalid reference inventory defers absence claims. This rule is separate from DRNARCHITECTURE003, which rejects forbidden references.

## Ownership

### DRNOWNERSHIP001: Declare business subdomain ownership

Roslyn category: `DRN.Conventions.Ownership`.

> '{0}' needs an unambiguous subdomain; use the conventional namespace or declare its owner.

Concrete Domain entities, including authentication users, and relevant bound repositories/application services need ownership. `Shop.Domain.Sales.Order` has Sales ownership. `Shop.Domain.Order` does not. Ordinary unbound helpers are excluded. Application orchestration can remain independent of `ISourceKnownService<T>`.

Use the conventional area or a reasoned exact ownership declaration. Identity is a valid technical owner. Abstract/open intermediates do not require a guessed business owner. External owner facts come from the declaring assembly or a permitted exact missing-fact declaration. Ownership never derives from an entity byte or account ID. Namespace moves need compatibility review.

### DRNOWNERSHIP002: Justify cross-subdomain entity use

Roslyn category: `DRN.Conventions.Ownership`.

> '{0}' in subdomain '{1}' references entity '{2}' from '{3}'. Use the entity's identifier or a contract from '{3}'.

Same-owner entity use across CQRS contexts is valid. Sales consuming an Inventory entity without a justified exception is reported. Strings, comments, using directives and unrelated same-named classes do not establish use. Bound signatures, inherited generic shapes and generated uses count when a handwritten declaration or exact input supplies an actionable location.

Consumers are scoped to Domain types, entities, persistence contexts/configurations, repository/service bindings, DI-registered Application/Infra services and SourceKnown controllers, including nested helpers. Page models, ordinary MVC controllers and unclassified navigation/utility types do not acquire subdomain ownership from namespaces. Page models may use domain entities. API contract exposure, project dependencies and implementation-role checks are separate rules.

Declaring assembly and symbol preserve ownership identity. Infra, Application and Hosted can use a referenced Domain entity in the same subdomain across naming prefixes. A Domain assembly using another Domain assembly's entity does not become the same owner merely because both subdomains share a name.

Use the owning contract/identifier, correct genuinely misplaced code/ownership, or justify the smallest exact reuse. Standard symbol suppressions cover that symbol. Pair-specific project exceptions additionally identify Entity/Context. Membership or ownership declarations alone never suppress cross-owner use. Fully generated consumers without a user anchor remain unreported. There is no automatic suppression-reason fix.

### DRNOWNERSHIP003: Establish the infrastructure's Domain subdomain

Roslyn category: `DRN.Conventions.Architecture`.

> Infrastructure '{0}' requires populated Domain subdomain '{1}'.

Every Infra project needs at least one populated subdomain namespace across its directly referenced Domain projects. Concrete Infra contexts and repositories must match a directly referenced Domain owner. Matching compares `(Domain, Subdomain)` pairs when either side uses `Folders` layout or an explicit domain declaration, and matches unannotated `Project`-layout owners by subdomain only. A type in `Shop.Domain.Sales` (Project layout) or `Platform.Domain.QA.Common` (Folders layout) establishes its owner in its declaring assembly. Same-named subdomains in different assemblies or different domains remain distinct owners. An empty directory or namespace with no types does not establish a subdomain. The message uses `<any>` when no populated Domain subdomain is available.

Place real Domain types under the owning subdomain or correct misplaced Infra ownership. Intentional project exceptions use `SymbolTarget="<AssemblyName>:Domain"` (or the reported symbol's documentation comment ID). Do not add placeholder types or empty folders merely to silence the diagnostic. Missing required references are reported first, and incomplete reference evidence does not prove a missing subdomain. No automatic namespace move or project-reference fix is offered.

### DRNOWNERSHIP004: Establish at least one Domain subdomain

Roslyn category: `DRN.Conventions.Ownership`.

> Domain project '{0}' requires at least one populated subdomain folder.

Every project carrying the Domain role (standalone Domain projects and split-role projects with a `Domain` role override) must contain at least one compiled non-generated type inside a valid subdomain folder (`<Subdomain>/<Folder>/` in Project layout or `<Domain>/<Subdomain>/<Folder>/` in Folders layout). Root zero-type files (such as `Usings.cs`) and root identity declarations (`EntityTypeAttribute<TApp>`, `IAppId`, `[DomainOnly]` `byte` enums) do not populate a subdomain on their own. Intentional project exceptions use `SymbolTarget="<AssemblyName>:Domain"`.

## Structure

### DRNSTRUCTURE001: Align namespace and logical placement

Roslyn category: `DRN.Conventions.Structure`.

> '{0}' belongs in '{1}' with namespace '{2}'; current logical placement conflicts.

Relevant handwritten declarations should agree with their role, owner and Compile/Link logical home. `Sales/Order.cs` in `Shop.Domain.Sales` is valid. A partial declaration in `Other/Order.More.cs` conflicts and is attached as additional evidence. Unclassified helpers are excluded. Ordinal case matters, while separator styles normalize.

Correct the reviewed namespace/path or use an exact justified placement exception. Split-role prefixes and Link paths are supported. Missing logical paths do not become physical-path guesses. No automatic move is offered because persisted context names, shared configuration discovery and routes can be affected.

### DRNSTRUCTURE002: Context name must have a nonempty Context stem

Roslyn category: `DRN.Conventions.Context`.

> Context '{0}' must have a nonempty name ending in 'Context'.

Concrete usable Infra contexts need a nonempty stem. Both `DbContext` subclasses and `IDrnContext` implementations are recognized. `SalesContext` and explicitly owned `SalesReadContext` are valid. `Store` and `Context` violate the suffix rule. An unrelated lookalike class is excluded, as are abstract/open intermediates.

Use an exact legacy exception when operational identity prevents renaming. An ownership alias does not waive this name check. No rename fix is provided. Review connection keys, schema and migration identity before changing a persisted context.

### DRNSTRUCTURE003: Declare conventional context destinations

Roslyn category: `DRN.Conventions.Structure`.

> Context '{0}' has no valid {1} destination. Set {1}="{2}" on its DrnConventionContext item, or provide a Reason for an alternate destination.

Conventional destinations are inferred without a `DrnConventionContext` entry. For a context in `Sample.Infra.Sales` whose logical source home is `Sales`, the defaults are `Sales/Repositories` and `Sales/Migrations`. All context partials must agree on that home. Existing context-bound repositories and migration/snapshot artifacts must have the corresponding namespace and logical directory. Compile/Link paths and split-role path prefixes are respected. No physical folder or placeholder file is required.

Repository inference applies to contexts exposing aggregate DbSets, including inherited sets. Migration inference applies when associated migration/snapshot artifacts exist. Explicit repository and migration destinations are validated even without artifacts. Empty contexts need no destination metadata.

Alternate, conflicting or unavailable logical homes require an explicit destination. Alternate destinations require a `Reason`. A separate migrations assembly requires explicit migration destination metadata. Explicit overrides do not waive folder, ownership or migration-association checks. No analyzer action generates migrations or changes a database.

### DRNSTRUCTURE004: Use the semantic role suffix

Roslyn category: `DRN.Conventions.Structure`.

> Rename '{0}' to end with '{1}' and include a name before the suffix.

Concrete aggregates end in Entity. Repository contracts and implementations end in Repository. SourceKnown services, entity-bound application services/contracts and eligible attribute-registered application services end in Service. MVC controllers end in Controller. IEntityTypeConfiguration implementations end in Config. The interface prefix I alone is not a name stem. Abstract classes, open generic helpers, Razor PageModels and unrelated lookalikes are excluded. Context names retain DRNSTRUCTURE002.

### DRNSTRUCTURE005: Place aggregates in their named directory and namespace

Roslyn category: `DRN.Conventions.Structure`.

> Place aggregate '{0}' in a folder named '{1}' and a namespace ending in '.{1}'.

OrderEntity belongs in a namespace ending in Order and a logical source directory ending in Order. Use Compile/Link paths, including every handwritten partial declaration. DRNSTRUCTURE001 still checks the full ownership path. Missing logical paths remain DRNCONVENTIONINPUT001 rather than guessed physical placement. Fix an invalid Entity suffix before placement is evaluated. No automatic public-type rename is offered.

### DRNSTRUCTURE006: Use the dedicated infrastructure folder

Roslyn category: `DRN.Conventions.Structure`.

> Place '{0}' in a folder named '{1}' and a namespace ending in '.{1}'.

Concrete Infra repository implementations belong immediately in Repositories and IEntityTypeConfiguration implementations in Configurations. Their final namespace segment must match. Domain interfaces stay beside aggregates. Migrations and model snapshots associated through DbContextAttribute with DrnContext or DrnContextIdentity descendants belong immediately in Migrations, including generated artifacts. Linked files use logical Compile/Link paths. Missing migration paths report incomplete input rather than guessing physical placement. Generated artifacts use a context or input reporting anchor when available. DRNPERSISTENCE006 still validates context identity and declared destination. Reasoned destination metadata does not waive this independent folder rule.

### DRNSTRUCTURE007: Name options for their context

Roslyn category: `DRN.Conventions.Context`.

> Rename options '{0}' to start with '{1}' and end with 'Options'. An 'Attribute' suffix may follow. Recommended name: '{1}NpgsqlDbContextOptionsAttribute'.

Custom NpgsqlDbContextOptionsAttribute descendants, including performance options, use the applied DRN context's exact name as their prefix. QAContextOptions and QAContextNpgsqlDbContextOptionsAttribute are valid for QAContext. Inherited custom attributes are checked against the receiving context too. Framework-provided shared defaults are exempt. Unapplied classes and arbitrary same-named options are not guessed to belong to a context. This rule does not require an Options folder or rename runtime settings automatically.

Prefer `QAContextNpgsqlDbContextOptionsAttribute`, applied as `[QAContextNpgsqlDbContextOptions]`. The `NpgsqlDbContext` portion is recommended, not mandatory.

### DRNSTRUCTURE008: Keep Domain grouping folders folders-only

Roslyn category: `DRN.Conventions.Structure`.

> '{0}' violates Domain folder rules: {1}.

Enforces the folders-only Domain grouping, project-root, and subdomain `_Shared` value-object rules in [ownership and persistence](CONVENTIONS.md#ownership-and-persistence). Placement is verified through logical `Compile`/`Link` paths across handwritten files and partial declarations, including split-role projects with a `Domain` `LogicalPathPrefix`. `_Shared` matching is case-insensitive and allows only canonical `_Shared` at the single subdomain level. Generated files are ignored. Intentional exceptions use the type's documentation comment ID, or the file's logical path (`SymbolTarget="<LogicalPath>"`) for zero-type files.

## EF Core persistence

### DRNPERSISTENCE001: Provide supported context construction

Roslyn category: `DRN.Conventions.Context`.

> Context '{0}' needs the options constructor required by its declared registration/design-time path.

DRN context bases require a public options-only `DbContextOptions<ActualContext>` constructor for their design-time factory. A public forwarding constructor is valid. A conventional context with only a parameterless constructor is not. For contexts without a DRN base, observed `AddDbContext` and `AddDbContextWithConventions` registrations accept public constructors taking `DbContextOptions<ActualContext>` or non-generic `DbContextOptions`, with optional additional injected dependencies. This does not establish that those dependencies are registered. Unselected custom construction paths are not guessed, and equivalent generic/new() compiler failures are not duplicated.

A reasoned exact Factory declaration can identify an alternate path, with runtime validation still required. The code fix adds only a known forwarding constructor. Primary constructors, conflicting overloads, nontrivial initialization or generated/linked/unedited partial ambiguity prevent the offer. Preview it before applying. It changes no settings, context identity or migrations.

### DRNPERSISTENCE002: Establish effective Npgsql convention

Roslyn category: `DRN.Conventions.Context`.

> Context '{0}' has no established Npgsql setup on its declared DRN configuration path; provide or declare the supported setup.

The bounded check recognizes a selected direct DRN options constructor that discards supplied options for null or fresh providerless options. Forwarding the configured options is valid. An empty local options callback does not prove provider absence. Arbitrary factories, helpers and custom OnConfiguring paths remain unknown.

Preserve the selected provider path. A reasoned custom Factory declaration identifies an alternative construction path whose behavior still needs validation. A Provider declaration records intent but does not waive proven discarded options. Do not add an empty customization attribute or invent connection strings to silence this rule. No automatic provider/settings fix is offered.

### DRNPERSISTENCE003: Preserve required base model configuration

Roslyn category: `DRN.Conventions.Context`.

> Override '{0}' has a proven path that omits the required base model configuration.

A real override must preserve required DRN/Identity setup. `base.OnModelCreating(model)` is valid. An empty override provably omits it. A hidden same-name method does not replace the EF virtual path. Supported straight-line forwarding helpers count, while arbitrary control flow/helpers remain unknown.

Restore required base setup or justify an exact intentional replacement with model evidence. No automatic base-call insertion is offered because order and replacement semantics matter. Quiet analysis is not a model-equivalence claim.

### DRNPERSISTENCE004: Make persistence assignment consistent

Roslyn category: `DRN.Conventions.Persistence`.

> Assignment of '{0}' to context '{1}' conflicts with '{2}'; resolve or declare the intended relationship.

Explicit inclusion and exclusion of the same entity/context pair conflict. One inclusion is valid, and the same owner can include an entity in multiple read/write contexts. Missing DbSets, navigation-only entities and unrelated aggregates do not establish exclusion. A same-named nonentity is not assigned automatically.

Resolve contradictory evidence or document the exact intended relationship. Membership metadata describes convention evidence and does not modify EF. Cross-owner use still needs its separate DRNOWNERSHIP002 exception. No fix selects membership or storage topology.

### DRNPERSISTENCE005: Make declared configuration discoverable

Roslyn category: `DRN.Conventions.Persistence`.

> Configuration '{0}' for '{1}' is not discoverable by context '{2}'; use its configuration namespace/assembly or an explicit supported application path.

Present/intended `IEntityTypeConfiguration<T>` mappings must be discoverable through supported evidence. The normal DRN scan includes exact/child context namespaces in its assembly. A sibling configuration associated with that context is invalid under a proven default scan. Explicit application and composed mappings can be valid. Unrelated Configure-named classes are excluded.

For a class implementing multiple `IEntityTypeConfiguration<T>` bindings, `ApplyConfiguration<TEntity>` applies only the selected entity's mapping. Other bindings still require supported discovery or application unless their entities are explicitly excluded or ignored.

An override that forwards the unchanged model builder to the framework base retains that default scan evidence, including an expression-bodied override. A straight-line override can begin with that single base call and then call `ApplyConfiguration<TEntity>(configuration)`, `Ignore<TEntity>()` or `Entity<TEntity>()` directly on the same builder. Unknown helper calls, callbacks and conditional setup do not establish the default scan.

Composed mappings support same-type, nonvirtual, nongeneric helper calls that forward the builder unchanged, including explicitly invoked local functions. Uninvoked helpers and deferred lambdas do not establish application.

Attribute-only mapping is valid with no configuration warning. Correct actual discovery, select an explicit supported association or justify an exact exceptional relationship. Unknown custom discovery is not declared broken. Never move shared mapping automatically. The separate Add entity configuration action preserves annotations and adds no invented relationships.

### DRNPERSISTENCE006: Align migrations and snapshot context identity

Roslyn category: `DRN.Conventions.Migrations`.

> Migration artifact '{0}' identifies context '{1}' inconsistently with declared destination '{2}'.

Known migrations/snapshots must match their declared context, assembly and logical destination. A matching snapshot is valid. A known snapshot under a conflicting destination is invalid. Unrelated classes with migration-like names are excluded. Generated artifacts still supply identity evidence.

Declare an intentional separate migration assembly/output path and review its compatibility. Do not rewrite historical migrations or snapshots to hide discrepancies. Compiler checks do not establish schema equivalence. EF-generated reconciliation and database actions retain separate execution/review gates.

### DRNPERSISTENCE007: Implement the application persistence context contract

Roslyn category: `DRN.Conventions.Context`.

> Application context '{0}' must implement IDrnContext or declare a narrow, justified exception.

Concrete application-owned EF DbContext subclasses must implement `IDrnContext` when conventions are enabled. An inherited implementation satisfies this policy. `DrnContext` inheritance is optional. Abstract bases are not reported, but their concrete descendants are checked. Concrete generic declarations are checked even though construction diagnostics require closed types. Referenced external context declarations are not application-owned source.

Implement the interface or use an exact reasoned suppression for an intentionally specialized or external integration context. The exception must explain why DRN persistence is intentionally unnecessary. A Factory or Provider reason does not waive this separate policy. Interface implementation alone does not certify provider setup, model configuration, ID validation, authorization or other DRN runtime safeguards. Context placement, public exposure and construction checks remain independent. No automatic inheritance or suppression fix is offered.

### DRNPERSISTENCE008: Expose aggregate roots through DbSet properties

Roslyn category: `DRN.Conventions.Persistence`.

> DbSet property '{0}' on context '{1}' exposes '{2}', which must derive from AggregateRoot.

Instance `DbSet<TEntity>` properties on `DrnContext<TContext>` descendants must expose framework `AggregateRoot` descendants. This includes inherited properties and closed generic substitutions. Child entities, ordinary classes and keyless rows do not qualify merely because EF can map them. Keep child entities behind their aggregate instead of exposing a context set, or document a narrow intentional exception.

The rule checks resolved framework symbols, including derived DbSet types. Static properties, unrelated lookalikes, plain DbContext and DrnContextIdentity are excluded. Open or erroneous entity types are deferred until resolved. Navigation-only entities and calls to Set/GetEntities are not DbSet property declarations. The rule changes neither EF mapping nor runtime generic constraints.

### DRNPERSISTENCE009: Declare context options on the context

Roslyn category: `DRN.Conventions.Context`.

> Context '{0}' must directly declare a custom attribute derived from NpgsqlDbContextOptionsAttribute. Recommended name: '{0}NpgsqlDbContextOptionsAttribute'.

Each concrete, closed `DrnContext` or `DrnContextIdentity` descendant must directly apply a custom attribute derived from `NpgsqlDbContextOptionsAttribute`. An attribute on another partial declaration counts. Inherited custom options and framework defaults do not satisfy the direct declaration requirement. Unrelated same-named attributes do not count. DRNSTRUCTURE007 independently checks the names of applied custom options.

Abstract bases, open generic contexts, plain EF contexts and unrelated classes are excluded. This rule requires a declaration, not a particular configuration override or runtime setting. Use the normal exact, justified exception channels for intentional departures.

## Repositories

### DRNREPOSITORY001: Align repository aggregate and context

Roslyn category: `DRN.Conventions.Repository`.

> Repository '{0}' binds aggregate '{1}' inconsistently with contract or context '{2}'.

Closed semantic repository bindings must agree with established persistence evidence. A repository for included Order is valid. Binding Order to a context that explicitly excludes or provably ignores it is invalid. A class named OrderRepository without the contract is excluded. Missing DbSets alone do not prove incompatibility.

Correct the actual binding or use an exact justified adapter/storage exception. Multiple aggregate interfaces are allowed when their bindings are valid. Unknown custom adapters are not assigned a guessed context. C# constraint and concrete-entity validity remain independent. No automatic aggregate/context substitution is offered.

### DRNREPOSITORY002: Align SourceKnown service binding

Roslyn category: `DRN.Conventions.Application`.

> Service '{0}' has inconsistent aggregate/repository binding '{1}'.

Supported `ISourceKnownService<T>` implementations must expose a repository for the same aggregate. Order service returning the Order repository is valid. A provably incompatible bound repository is invalid. Ordinary orchestration across aggregates without that contract remains valid, as do Hosted SourceKnown controllers in their transport role.

Correct the real member/constructor binding or justify an exact adapter contract. Properly typed explicit multi-interface implementations are supported. Arbitrary factories and method bodies remain unknown. No empty service or mandatory framework base is introduced.

### DRNREPOSITORY003: Declare the aggregate repository interface

Roslyn category: `DRN.Conventions.Repository`.

> Aggregate '{0}' has no matching repository interface in Domain subdomain '{1}'. Declare or move an interface extending ISourceKnownRepository<{0}> into the same namespace and folder as '{0}'.

Each concrete, closed Domain aggregate needs a dedicated interface derived directly or indirectly from ISourceKnownRepository of that exact aggregate, in its own assembly, namespace and logical folder. An interface for another aggregate, namespace or folder does not satisfy the rule. Every handwritten partial declaration must share the folder. Compile/Link paths determine logical placement. Missing or invalid paths are handled by ownership input diagnostics without guessing physical paths. Referenced assemblies expose namespace identity, so producer folder validation occurs in the Domain compilation. Abstract/open generic aggregates are excluded. Missing or invalid ownership is handled by ownership diagnostics.

### DRNREPOSITORY004: Implement the aggregate repository in infrastructure

Roslyn category: `DRN.Conventions.Repository`.

> Aggregate '{0}' has no matching repository in Infra subdomain '{1}'. Add a concrete SourceKnownRepository implementation of its Domain repository interface.

Checks only aggregates assigned to this Infra project's concrete contexts through DbSets, explicit inclusion or supported configuration evidence. Requires a concrete, closed SourceKnownRepository descendant implementing the exact Domain interface in the aggregate's subdomain. An unrelated same-named class, an abstract repository, or an implementation of only the framework interface is insufficient. One implementation can satisfy multiple contexts in that Infra. Explicitly excluded/ignored entities and unassigned Domain aggregates do not create an obligation. Reports at a context with the exact Entity property (and optional `Context` qualifier) for narrow exceptions. This is bounded source evidence, not execution of arbitrary model-building code.

## Dependency injection

### DRNDEPENDENCYINJECTION001: Establish effective service/repository registration

Roslyn category: `DRN.Conventions.Registration`.

> '{0}' has no effective registration for declared service '{1}' on the supported composition path.

Explicit registration demands must match their implementation, service and key on a supported observed path. Coverage also includes eligible ordinary services and repositories without explicit demands. The [service eligibility boundary](CONVENTIONS.md#service-registration-evidence) covers paired behavioral interfaces and unambiguous required constructor dependencies. Inherited `[Scoped<IOrderService>]` and a matching supported direct registration are valid. An eligible service in a proven attribute scan without an effective binding is invalid. A user-defined Scoped lookalike does not count.

Ordinary Application orchestration and repositories need no SourceKnown base. Data-only interfaces and arbitrary helpers do not establish service intent. Missing local attributes alone do not establish missing registration. Unknown custom modules/helpers defer absence claims. Exact demands distinguish unkeyed and keyed registrations, including empty-string keys. An exact reasoned custom registration or suppression remains a runtime verification obligation. Use the application's established composition path rather than duplicating attributed registration. No fix guesses lifetime, interface or key.

### DRNDEPENDENCYINJECTION002: Declare a DI lifetime for paired implementations

Roslyn category: `DRN.Conventions.Registration`.

> '{0}' must register '{1}' through a matching LifetimeAttribute. Choose [Scoped<{1}>], [Transient<{1}>], or [Singleton<{1}>] for its intended lifetime.

DRNDEPENDENCYINJECTION002 takes precedence over DRNDEPENDENCYINJECTION001 for the same implementation/interface binding failure. Other service demands and key mismatches remain eligible for DRNDEPENDENCYINJECTION001. A missing attribute scan is not diagnosed by either rule.

Concrete, closed Application/Infra service implementations of an exact `I{Name}` interface require one lifetime attribute bound to that exact interface, including its generic arguments. Generic lifetime derivatives establish the binding through their service type argument. Custom nongeneric derivatives also count when source-visible explicit constructor chains forward a statically known type to LifetimeAttribute's serviceType parameter. Supported evidence includes typeof expressions, type-valued attribute arguments, parameter forwarding, named arguments, primary constructors and explicit base/this initializers. Arbitrary expressions and unavailable constructor bodies remain unproven. A nongeneric attribute name alone does not establish a binding. Direct lifetime attributes take precedence over inherited ones, matching runtime scanning. Multiple selected lifetime attributes, self-registration and other interface bindings do not satisfy the rule. Partial declarations are checked once. Choose a lifetime appropriate to the implementation's dependencies. The examples are alternatives, not interchangeable recommendations.

The paired interface must have a recognized Application, Domain, Infra, SharedKernel, Utils, Contract or Hosted role. Unrecognized interfaces such as `System.IDisposable` do not establish pairing. Role recognition does not permit otherwise forbidden project references. An eligible `HostedServiceAttribute` implementation satisfies an `IHostedService` pair through its hosted-service registration. It does not satisfy unrelated interfaces or keyed `IHostedService` demands.

This binding rule applies without an observed attribute scan. Manual registrations and unknown custom composition do not waive it. Keyed matching bindings count. DRNDEPENDENCYINJECTION001 independently checks required keys and supported registration paths. DRNDEPENDENCYINJECTION002 does not certify runtime registration or lifetime safety. Abstract/open generic classes, entities, neutral values, DTOs, contexts, EF configurations, bound options and transport types such as Razor page models and controllers are excluded. Use an exact, justified convention exception for intentional departures.

### DRNDEPENDENCYINJECTION003: Declare a project module with an attribute scan

Roslyn category: `DRN.Conventions.Registration`.

> Project '{0}' needs a public static *Module class with a public static method accepting and returning IServiceCollection. Call services.AddServicesWithAttributes() on that parameter to scan this project's assembly.

Hosted, Application, Infra and Utils projects need a public, top-level, nongeneric static class whose name ends in `Module`. It must expose a public static nongeneric method accepting and returning `IServiceCollection`, with a direct `services.AddServicesWithAttributes()` call on its service-collection parameter. A static call to the same DRN API also counts. An explicit `typeof(LocalType).Assembly` argument is supported when the type belongs to this assembly.

Calls scanning another assembly, lookalike APIs, calls on unrelated collections and calls inside lambdas or local functions do not satisfy the rule. Framework/test projects remain excluded by project-role discovery. Domain, Contract and SharedKernel roles do not require DI modules. The diagnostic reports with `Location.None` and accepts project-level exceptions using `SymbolTarget="<AssemblyName>:Module"` (for example, `Shop.Application:Module`). This is a structural check. It does not prove host invocation, reachability or execution on every path.

```csharp
public static class ApplicationModule
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddServicesWithAttributes();
        return services;
    }
}
```

## Convention inputs

### DRNCONVENTIONINPUT001: Invalid or incomplete convention input

Roslyn category: `DRN.Conventions.Configuration`.

> Convention input '{0}' is {1}. {2}.

For a context/subdomain mismatch, prefer either conventional alignment before an alias: `DataProtection` with `DataProtectionContext`, or `DrnDataProtection` with `DrnDataProtectionContext`, with matching namespace and folder. Use a reasoned mapping only for an intentional exception.

The analyzer needs an unambiguous project role and consistent ownership and logical-path evidence. Case-insensitive whole role tokens identify roles. Both `Infra` and `Shop.Infrastructure.Sql` are Infra, while `DomainHelpers` is unrecognized. No application-name prefix is required. A name with multiple role tokens is ambiguous. Conflicting owners, overlapping split roles, invalid paths or missing reference inventory are also invalid.

Use an unambiguous conventional project name or correct the reported compiler input. Custom activation/root/role properties do not establish identity. Unknown dependent facts remain unavailable. Input locations identify the declaration, while messages omit physical source paths. No automatic input fix is offered.

## Convention exceptions

### DRNCONVENTIONEXCEPTION001: Make observable exceptions narrow and reasoned

Roslyn category: `DRN.Conventions.Exceptions`.

> Exception for '{0}' is {1}; provide an exact target, meaningful reason and required ownership metadata.

A project exception needs an exact target, meaningful reason, owner and future review date. Standard symbol SuppressMessage declarations need an exact diagnostic and justification but do not require project owner/date fields. Wildcards, namespace-wide assembly or module suppressions, blank reasons, due project review dates and changed fingerprints are invalid. Unrelated diagnostic suppressions are left alone. Named paired pragmas need a concrete adjacent reason and a narrow region.

For an ordinary C# property named `Item`, place `[SuppressMessage]` directly on the property or use a `DrnConventionException` with its exact property ID. Roslyn interprets `Item` as an indexer name when resolving an assembly or module suppression by documentation ID.

Correct or remove the exception. Never fabricate a justification. Optional fingerprints bind reviewed declaration text and logical paths, so moves/edits need review. CI/editorconfig/command-line disabling cannot be universally audited. This rule is not a tamper-proof security boundary, and suppressing it does not make an invalid exception valid.
