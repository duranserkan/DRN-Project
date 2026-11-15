# SourceKnown diagnostic reference

All rules below are **Warning** diagnostics enabled for application projects detected from naming conventions. They can fail warnings-as-errors builds. Standard `.editorconfig` severity settings and narrow reasoned suppressions control intentional exceptions. Existing identity, classification and authorization diagnostics remain active independently. See [project identity, inputs and exceptions](CONVENTIONS.md) and [editable assistance](SCAFFOLDING.md).

Messages show format placeholders, replaced by actual symbols or facts. Project roles use documented name tokens. Type checks use resolved types, so a class merely named Entity, Repository or Context does not qualify. No rule changes runtime behavior or certifies EF models, DI, serialization or authorization.

There is no Fix All. Only DRNCV005 has a diagnostic code fix. Configuration creation is an optional refactoring on valid code. Other remedies require explicit intent and compatibility review. Exact reasoned exceptions use the channels documented in CONVENTIONS.md. Suppressions do not waive other diagnostics or runtime safeguards.

## DRNCV001: Invalid or incomplete convention input

Category: `DRN.Conventions.Configuration`.

> Convention input '{0}' is {1}; declare one valid {2}.

The analyzer needs an unambiguous project role and consistent ownership and logical-path evidence. Case-insensitive whole role tokens identify roles. Both `Infra` and `Shop.Infrastructure.Sql` are Infra, while `DomainHelpers` is unrecognized. No application-name prefix is required. A name with multiple role tokens is ambiguous. Conflicting owners, overlapping split roles, invalid paths or missing reference inventory are also invalid.

Use an unambiguous conventional project name or correct the reported compiler input. Custom activation/root/role properties do not establish identity. Unknown dependent facts remain unavailable. Input locations identify the declaration, while messages omit physical source paths. A bounded public-shape traversal can also report incomplete evidence here. Review that model instead of treating quiet analysis as safety. No automatic input fix is offered.

## DRNCV002: Declare business subdomain ownership

Category: `DRN.Conventions.Ownership`.

> '{0}' needs an unambiguous subdomain; use the conventional namespace or declare its owner.

Concrete Domain entities, including authentication users, and relevant bound repositories/application services need ownership. `Shop.Domain.Sales.Order` has Sales ownership. `Shop.Domain.Order` does not. Ordinary unbound helpers are excluded. Application orchestration can remain independent of `ISourceKnownService<T>`.

Use the conventional area or a reasoned exact ownership declaration. Identity is a valid technical owner. Abstract/open intermediates do not require a guessed business owner. External owner facts come from the declaring assembly or a permitted exact missing-fact declaration. Ownership never derives from an entity byte or account ID. Namespace moves need compatibility review.

## DRNCV003: Align namespace and logical placement

Category: `DRN.Conventions.Structure`.

> '{0}' belongs in '{1}' with namespace '{2}'; current logical placement conflicts.

Relevant handwritten declarations should agree with their role, owner and Compile/Link logical home. `Sales/Order.cs` in `Shop.Domain.Sales` is valid. A partial declaration in `Other/Order.More.cs` conflicts and is attached as additional evidence. Unclassified helpers are excluded. Ordinal case matters, while separator styles normalize.

Correct the reviewed namespace/path or use an exact justified placement exception. Split-role prefixes and Link paths are supported. Missing logical paths do not become physical-path guesses. No automatic move is offered because persisted context names, shared configuration discovery and routes can be affected.

## DRNCV004: Context name must have a nonempty Context stem

Category: `DRN.Conventions.Context`.

> Context '{0}' must have a nonempty name ending in 'Context'.

Concrete usable Infra contexts need a nonempty stem. Both `DbContext` subclasses and `IDrnContext` implementations are recognized. `SalesContext` and explicitly owned `SalesReadContext` are valid. `Store` and `Context` violate the suffix rule. An unrelated lookalike class is excluded, as are abstract/open intermediates.

Use an exact legacy exception when operational identity prevents renaming. An ownership alias does not waive this name check. No rename fix is provided. Review connection keys, schema and migration identity before changing a persisted context.

## DRNCV005: Provide supported context construction

Category: `DRN.Conventions.Context`.

> Context '{0}' needs the options constructor required by its declared registration/design-time path.

DRN context bases require a public options-only `DbContextOptions<ActualContext>` constructor for their design-time factory. A public forwarding constructor is valid. A conventional context with only a parameterless constructor is not. For contexts without a DRN base, observed `AddDbContext` and `AddDbContextWithConventions` registrations also accept public options constructors with additional injected dependencies. This does not establish that those dependencies are registered. Unselected custom construction paths are not guessed, and equivalent generic/new() compiler failures are not duplicated.

A reasoned exact Factory declaration can identify an alternate path, with runtime validation still required. The code fix adds only a known forwarding constructor. Primary constructors, conflicting overloads, nontrivial initialization or generated/linked/unedited partial ambiguity prevent the offer. Preview it before applying. It changes no settings, context identity or migrations.

## DRNCV006: Establish effective Npgsql convention

Category: `DRN.Conventions.Context`.

> Context '{0}' has no established Npgsql setup on its declared DRN configuration path; provide or declare the supported setup.

The bounded check recognizes a selected direct DRN options constructor that discards supplied options for null or fresh providerless options. Forwarding the configured options is valid. An empty local options callback does not prove provider absence. Arbitrary factories, helpers and custom OnConfiguring paths remain unknown.

Preserve the selected provider path. A reasoned custom Factory declaration identifies an alternative construction path whose behavior still needs validation. A Provider declaration records intent but does not waive proven discarded options. Do not add an empty customization attribute or invent connection strings to silence this rule. No automatic provider/settings fix is offered.

## DRNCV007: Preserve required base model configuration

Category: `DRN.Conventions.Context`.

> Override '{0}' has a proven path that omits the required base model configuration.

A real override must preserve required DRN/Identity setup. `base.OnModelCreating(model)` is valid. An empty override provably omits it. A hidden same-name method does not replace the EF virtual path. Supported straight-line forwarding helpers count, while arbitrary control flow/helpers remain unknown.

Restore required base setup or justify an exact intentional replacement with model evidence. No automatic base-call insertion is offered because order and replacement semantics matter. Quiet analysis is not a model-equivalence claim.

## DRNCV008: Make persistence assignment consistent

Category: `DRN.Conventions.Persistence`.

> Assignment of '{0}' to context '{1}' conflicts with '{2}'; resolve or declare the intended relationship.

Explicit inclusion and exclusion of the same entity/context pair conflict. One inclusion is valid, and the same owner can include an entity in multiple read/write contexts. Missing DbSets, navigation-only entities and unrelated aggregates do not establish exclusion. A same-named nonentity is not assigned automatically.

Resolve contradictory evidence or document the exact intended relationship. Membership metadata describes convention evidence and does not modify EF. Cross-owner use still needs its separate DRNCV019 exception. No fix selects membership or storage topology.

## DRNCV009: Make declared configuration discoverable

Category: `DRN.Conventions.Persistence`.

> Configuration '{0}' for '{1}' is not discoverable by context '{2}'; use its configuration namespace/assembly or an explicit supported application path.

Present/intended `IEntityTypeConfiguration<T>` mappings must be discoverable through supported evidence. The normal DRN scan includes exact/child context namespaces in its assembly. A sibling configuration associated with that context is invalid under a proven default scan. Explicit application and composed mappings can be valid. Unrelated Configure-named classes are excluded.

Composed mappings support same-type, nonvirtual, nongeneric helper calls that forward the builder unchanged, including explicitly invoked local functions. Uninvoked helpers and deferred lambdas do not establish application.

Attribute-only mapping is valid with no configuration warning. Correct actual discovery, select an explicit supported association or justify an exact exceptional relationship. Unknown custom discovery is not declared broken. Never move shared mapping automatically. The separate Add entity configuration action preserves annotations and adds no invented relationships.

## DRNCV010: Declare conventional context destinations

Category: `DRN.Conventions.Structure`.

> Context '{0}' needs declared {1} destination metadata matching '{2}', or a reasoned override.

Concrete persistent contexts need tracked Repositories and Migrations destinations even before those artifacts exist. `Sales/Repositories` and `Sales/Migrations` are valid for a conventional Sales area. Missing or conflicting destinations are invalid. Ordinary classes named Context do not require destinations.

Add the exact `DrnConventionContext` item or a reasoned alternate destination/migrations assembly. Do not create fake files for empty folders. Project metadata edits need a supported atomic host transaction or reviewed template plan. No analyzer action generates migrations or changes a database.

## DRNCV011: Align repository aggregate and context

Category: `DRN.Conventions.Repository`.

> Repository '{0}' binds aggregate '{1}' inconsistently with contract or context '{2}'.

Closed semantic repository bindings must agree with established persistence evidence. A repository for included Order is valid. Binding Order to a context that explicitly excludes or provably ignores it is invalid. A class named OrderRepository without the contract is excluded. Missing DbSets alone do not prove incompatibility.

Correct the actual binding or use an exact justified adapter/storage exception. Multiple aggregate interfaces are allowed when their bindings are valid. Unknown custom adapters are not assigned a guessed context. C# constraint and concrete-entity validity remain independent. No automatic aggregate/context substitution is offered.

## DRNCV012: Establish effective service/repository registration

Category: `DRN.Conventions.Registration`.

> '{0}' has no effective registration for declared service '{1}' on the supported composition path.

Explicit registration demands must match their implementation, service and key on a supported observed path. Coverage also includes eligible ordinary services and repositories without explicit demands. The [service eligibility boundary](CONVENTIONS.md#service-registration-evidence) covers paired behavioral interfaces and unambiguous required constructor dependencies. Inherited `[Scoped<IOrderService>]` and a matching supported direct registration are valid. An eligible service in a proven attribute scan without an effective binding is invalid. A user-defined Scoped lookalike does not count.

Ordinary Application orchestration and repositories need no SourceKnown base. Data-only interfaces and arbitrary helpers do not establish service intent. Missing local attributes alone do not establish missing registration. Unknown custom modules/helpers defer absence claims. Exact demands distinguish unkeyed and keyed registrations, including empty-string keys. An exact reasoned custom registration or suppression remains a runtime verification obligation. Use the application's established composition path rather than duplicating attributed registration. No fix guesses lifetime, interface or key.

## DRNCV013: Align SourceKnown service binding

Category: `DRN.Conventions.Application`.

> Service '{0}' has inconsistent aggregate/repository binding '{1}'.

Supported `ISourceKnownService<T>` implementations must expose a repository for the same aggregate. Order service returning the Order repository is valid. A provably incompatible bound repository is invalid. Ordinary orchestration across aggregates without that contract remains valid, as do Hosted SourceKnown controllers in their transport role.

Correct the real member/constructor binding or justify an exact adapter contract. Properly typed explicit multi-interface implementations are supported. Arbitrary factories and method bodies remain unknown. No empty service or mandatory framework base is introduced.

## DRNCV014: Keep external contracts free of persistence types

Category: `DRN.Conventions.Contract`.

> Public contract member '{0}' exposes persistence/domain type '{1}'; expose an approved DTO or value model.

Public Contract and supported transport shapes should expose DTOs/values rather than entities, contexts or repository objects. Dto Guid/timestamp mapping is valid. A nested array/tuple/generic wrapper containing Order is invalid. An unrelated value class named Entity is not a persistence type.

An exact Dto constructor forwarding its source entity is allowed. Private nonserialized framework state is not external API. Approved neutral values remain eligible for public-shape inspection rather than hiding nested entities. Models exceeding the analysis limit report incomplete evidence and require review. Object/dynamic/custom serialization requires application review. Any DTO/API rewrite needs explicit compatibility and field decisions.

## DRNCV015: Respect direct role references

Category: `DRN.Conventions.Architecture`.

> Direct {0} reference from '{1}' to '{2}' violates the declared role matrix; remove it or document the exact exception.

Direct provenance distinguishes declared dependencies from transitive compiler availability. Application to Infra is valid. Domain to Infra is not. All application roles may depend inward on SharedKernel, which cannot depend outward on application layers. Roles come from exported project names or, when absent, actual resolved assembly names. Package IDs do not establish project roles. Exact known framework packages retain their documented restrictions. See the [dependency rules](CONVENTIONS.md#progressive-dependency-rules).

Remove the actual direct edge or justify its exact project/package target. Split roles do not gain a union of privileges. Missing direct-reference provenance remains incomplete evidence even when the resolved assembly has an inferred role. No fix changes package/project references.

## DRNCV016: Place semantic implementations in their owning role

Category: `DRN.Conventions.Architecture`.

> Implementation '{0}' ({1}) belongs to role '{2}'.

Contexts/configurations/repository implementations belong in Infra, entities/repository contracts in Domain, DTOs in Contract, services in Application and transport in Hosted. Bound implementation use also counts. DbContext, ModelBuilder or EntityTypeBuilder use in Domain is invalid. Lightweight EF Owned/Index-style mapping attributes remain valid.

SharedKernel may contain shared contracts and abstract entity, DTO and service bases. Concrete application entities and service implementations still belong in their owning layers. SharedKernel is not a persistence or transport layer.

Correct the role/placement or justify an exact exceptional implementation. Utils cannot hide application implementation types. A SourceKnown controller remains transport even when it also implements a service interface. Semantic lookalikes are excluded. Moves are not automated because assembly discovery and public contracts can change.

## DRNCV017: Align migrations and snapshot context identity

Category: `DRN.Conventions.Migrations`.

> Migration artifact '{0}' identifies context '{1}' inconsistently with declared destination '{2}'.

Known migrations/snapshots must match their declared context, assembly and logical destination. A matching snapshot is valid. A known snapshot under a conflicting destination is invalid. Unrelated classes with migration-like names are excluded. Generated artifacts still supply identity evidence.

Declare an intentional separate migration assembly/output path and review its compatibility. Do not rewrite historical migrations or snapshots to hide discrepancies. Compiler checks do not establish schema equivalence. EF-generated reconciliation and database actions retain separate execution/review gates.

## DRNCV018: Make observable exceptions narrow and reasoned

Category: `DRN.Conventions.Exceptions`.

> Exception for '{0}' is {1}; provide an exact target, meaningful reason and required ownership metadata.

A project exception needs an exact target, meaningful reason, owner and future review date. Standard symbol SuppressMessage declarations need an exact diagnostic and justification but do not require project owner/date fields. Wildcards, namespace-wide assembly suppressions, blank reasons, due project review dates and changed fingerprints are invalid. Unrelated diagnostic suppressions are left alone. Named paired pragmas need a concrete adjacent reason and a narrow region.

Correct or remove the exception. Never fabricate a justification. Optional fingerprints bind reviewed declaration text and logical paths, so moves/edits need review. CI/editorconfig/command-line disabling cannot be universally audited. This rule is not a tamper-proof security boundary, and suppressing it does not make an invalid exception valid.

## DRNCV019: Justify cross-subdomain entity use

Category: `DRN.Conventions.Ownership`.

> '{0}' in subdomain '{1}' uses entity '{2}' owned by '{3}'. Use an owning-subdomain contract or identifier, correct misplaced ownership/code, or add a narrow DRNCV019 suppression with a concrete justification for intentional reuse.

Same-owner entity use across CQRS contexts is valid. Sales consuming an Inventory entity without a justified exception is reported. Strings, comments, using directives and unrelated same-named classes do not establish use. Bound signatures, inherited generic shapes and generated uses count when a handwritten declaration or exact input supplies an actionable location.

Declaring assembly and symbol preserve ownership identity. Infra, Application and Hosted can use a referenced Domain entity in the same subdomain across naming prefixes. A Domain assembly using another Domain assembly's entity does not become the same owner merely because both subdomains share a name.

Use the owning contract/identifier, correct genuinely misplaced code/ownership, or justify the smallest exact reuse. Standard symbol suppressions cover that symbol. Pair-specific project exceptions additionally identify Entity/Context. Membership or ownership declarations alone never suppress cross-owner use. Fully generated consumers without a user anchor remain unreported. There is no automatic suppression-reason fix.

## DRNCV020: Implement the application persistence context contract

Category: `DRN.Conventions.Context`.

> Application context '{0}' must implement IDrnContext or declare a narrow, justified exception.

Concrete application-owned EF DbContext subclasses must implement `IDrnContext` when conventions are enabled. An inherited implementation satisfies this policy. `DrnContext` inheritance is optional. Abstract bases are not reported, but their concrete descendants are checked. Concrete generic declarations are checked even though construction diagnostics require closed types. Referenced external context declarations are not application-owned source.

Implement the interface or use an exact reasoned suppression for an intentionally specialized or external integration context. The exception must explain why DRN persistence is intentionally unnecessary. A Factory or Provider reason does not waive this separate policy. Interface implementation alone does not certify provider setup, model configuration, ID validation, authorization or other DRN runtime safeguards. Context placement, public exposure and construction checks remain independent. No automatic inheritance or suppression fix is offered.

## DRNCV021: Add the required direct layer reference

Category: `DRN.Conventions.Architecture`.

> Project '{0}' requires a direct project reference to its '{1}' layer.

Hosted projects require at least one direct Application and one direct Infra project reference. Infra requires at least one Domain. Domain requires at least one Contract. These requirements apply whenever the corresponding layer exists, even before its code uses the dependency. Naming prefixes do not constrain references. Multiple Hosted projects may share Infra, and multiple Domain or Contract references are valid. Packages and transitive references do not satisfy a direct project requirement. Every direct reference must still obey DRNCV015's allowed direction.

Add the missing `ProjectReference` or correct a misidentified project name. The analyzer does not create projects or references. Incomplete or invalid reference inventory defers absence claims. This rule is separate from DRNCV015, which rejects forbidden references.

## DRNCV022: Establish the corresponding Domain subdomain

Category: `DRN.Conventions.Architecture`.

> Infrastructure '{0}' requires populated Domain subdomain '{1}'.

Every Infra project needs at least one populated subdomain namespace across its directly referenced Domain projects. Concrete Infra contexts and repositories must have matching Domain subdomains. A type in `Shop.Domain.Sales` establishes Sales in its declaring assembly. Same-named subdomains in different assemblies remain distinct owners. An empty directory or namespace with no types does not establish a subdomain. The message uses `<any>` when no populated Domain subdomain is available.

Place real Domain types under the owning subdomain or correct misplaced Infra ownership. Do not add placeholder types or empty folders merely to silence the diagnostic. Missing required references are reported first, and incomplete reference evidence does not prove a missing subdomain. No automatic namespace move or project-reference fix is offered.
