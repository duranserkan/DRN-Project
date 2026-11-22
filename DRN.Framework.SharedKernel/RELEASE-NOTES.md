Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.11.0

### Security

*   **JSON integer errors**: Invalid `long` and `long?` values produce a conversion error that omits the submitted value.

### Breaking Changes

*   **Domain and subdomain ownership, strict public contracts, entity-owned `ToDto()`, and enum rules**:
    *   **Ownership layout**: Added `<DrnDomainLayout>` (`Project` default or `Folders`) and `<DrnDomain>` MSBuild properties exported to assembly metadata, plus optional `Domain` on `[SourceKnownSubdomain]`, `DrnConventionOwnership`, and `DrnConventionContext`. `DRNSTRUCTURE008` requires Domain grouping folders to contain only folders and reserves subdomain-scoped `_Shared` strictly for value objects (never entities, repositories, services, enums, interfaces, delegates, static classes, or persistence types), allowing only root zero-type files (such as `Usings.cs`), `EntityTypeAttribute<TApp>`, `IAppId`, and `[DomainOnly]` `byte` enums referenced by a local `EntityTypeAttribute` subclass at the project root. Zero-type file exceptions support logical-path `SymbolTarget` and paired `#pragma warning` directives. `DRNOWNERSHIP004` requires every project carrying the Domain role to contain at least one populated subdomain folder.
    *   **Entity-owned DTO projection and conventions**: Added `public abstract Dto ToDto()` on `SourceKnownEntity`. `DRNDTO001` through `DRNDTO003` check entity-owned `ToDto()` overrides returning the public Contract `{Stem}Dto` via `new(this)`, Contract DTO placement (exposing any subset of properties, including metadata only), and `[Personal]`/`[Sensitive]` classification preservation across entity `ToDto()` and Contract `*Response` projections (following nested assignments, collection elements, dictionary keys/values, inherited properties, type classifications, visible helpers, and generic/abstract mappers independently of `DRNDTO001` exceptions). `DRNDTO004` warns when a public entity-prefixed request or response is outside the matching DTO's folder or namespace. `DRNDTO007` verifies that concrete `SourceKnownService<TEntity, TDto>` subclasses inheriting default `ToDto(TEntity)` align `TDto` with `TEntity.ToDto()`. The selected-property IDE action requires a physically contained entity file, emits `public override {Stem}Dto ToDto()` automatically, preserves existing mapper calls, and declines `[DomainOnly]` enum properties.
    *   **Strict public contracts**: Added `DRNDTO005` (prohibiting `Dto` exposure, inheritance, or nesting in public Contract messages and controller endpoints, honoring `[NonAction]`/`[NonController]` and ignoring `[FromServices]`/`[FromKeyedServices]`) and `DRNDTO006` (requiring explicit Contract `*Request` and `*Response` types on public endpoints across return types, `[ProducesResponseType]`, and complex `[FromQuery]` parameters, with scalar, pagination, status-result, `IResult`, and 204 exemptions).
    *   **Enum ownership and zero opt-in**: Added `[DomainOnly]` and `[AllowZero]` attributes and diagnostics `DRNENUM001` through `DRNENUM005`. Application enums belong in Contract by default. Domain enums require `[DomainOnly]` and cannot appear on public contracts, endpoints, or effective `[ProducesResponseType]` metadata on actions and controllers. Any Contract or Domain enum defining a `0` member (including implicit, computed, aliased, later, `[Flags]`, and `[DomainOnly]` zeros) requires `[AllowZero]`, and `[AllowZero]` requires a named zero member.

*   **Entity type declarations**: DRNENTITY008 now requires a domain attribute with a byte-backed enum parameter. Replace direct `[EntityType<TApp>(n)]`, `[TestEntityType(n)]` and raw-byte wrappers in domain and test projects. Give each enum member its existing numeric value and retain the application partition to preserve stored IDs. Referenced domain assemblies compiled with byte-based annotations also report DRNENTITY008, naming the assembly and entity, until they are rebuilt.

*   **Diagnostic IDs**: All analyzer diagnostic IDs now use domain-prefixed names. Use the mappings below to update ID-specific entries in `.editorconfig`, `NoWarn`, `WarningsAsErrors`, paired `#pragma warning` directives, `SuppressMessage` attributes, and `DrnConventionException` MSBuild items.
    *   **Entity**: Replace `DRN0001` through `DRN0014` with `DRNENTITY001` through `DRNENTITY014`, keeping the same numeric value (for example, `DRN0006` becomes `DRNENTITY006`).
    *   **Classification and authorization**: `DRN0015` through `DRN0025` become `DRNCLASSIFICATION001` through `DRNCLASSIFICATION011`, `DRN0026` becomes `DRNAUTHORIZATION001`, and `DRN0027` becomes `DRNCLASSIFICATION012`.
    *   **Hosting**: `DRNHG001` becomes `DRNHOSTING001`, and `DRNHG002` becomes `DRNHOSTING002`.
    *   **Conventions**:
        *   `DRNCV001` splits into `DRNCONVENTIONINPUT001` for invalid or incomplete convention inputs and `DRNARCHITECTURE001` for public contract inspection limits. `DRNCV018` -> `DRNCONVENTIONEXCEPTION001`.
        *   `DRNCV002` -> `DRNOWNERSHIP001`, `DRNCV019` -> `DRNOWNERSHIP002`, `DRNCV022` -> `DRNOWNERSHIP003`
        *   `DRNCV003` -> `DRNSTRUCTURE001`, `DRNCV004` -> `DRNSTRUCTURE002`, `DRNCV010` -> `DRNSTRUCTURE003`, `DRNCV024` -> `DRNSTRUCTURE004`, `DRNCV025` -> `DRNSTRUCTURE005`, `DRNCV028` -> `DRNSTRUCTURE006`, `DRNCV029` -> `DRNSTRUCTURE007`
        *   `DRNCV005` -> `DRNPERSISTENCE001`, `DRNCV006` -> `DRNPERSISTENCE002`, `DRNCV007` -> `DRNPERSISTENCE003`, `DRNCV008` -> `DRNPERSISTENCE004`, `DRNCV009` -> `DRNPERSISTENCE005`, `DRNCV017` -> `DRNPERSISTENCE006`, `DRNCV020` -> `DRNPERSISTENCE007`, `DRNCV023` -> `DRNPERSISTENCE008`, `DRNCV030` -> `DRNPERSISTENCE009`
        *   `DRNCV011` -> `DRNREPOSITORY001`, `DRNCV013` -> `DRNREPOSITORY002`, `DRNCV026` -> `DRNREPOSITORY003`, `DRNCV027` -> `DRNREPOSITORY004`
        *   `DRNCV012` -> `DRNDEPENDENCYINJECTION001`, `DRNCV031` -> `DRNDEPENDENCYINJECTION002`, `DRNCV032` -> `DRNDEPENDENCYINJECTION003`
        *   `DRNCV014` -> `DRNARCHITECTURE002`, `DRNCV015` -> `DRNARCHITECTURE003`, `DRNCV021` -> `DRNARCHITECTURE006`
        *   `DRNCV016` splits into `DRNARCHITECTURE004` for misplaced implementations and `DRNARCHITECTURE005` for forbidden layer dependencies. Migrate settings and exceptions to the rule that matches their intent.

*   **Partition producers**: DRNENTITY006 and DRNENTITY007 also check concrete `IAppId` implementations in projects that declare no entities. Publish a constant partition from 0 through 127 and return it from the getter implementing `IAppId.AppId`.

*   **Project DI modules**: DRNDEPENDENCYINJECTION003 requires Hosted, Application, Infra and Utils projects to expose a public static module method accepting and returning IServiceCollection and scanning the project's assembly for attributed services. Domain, Contract, SharedKernel, framework and test projects are excluded. This checks module structure rather than runtime invocation.

*   **Custom DI bindings**: Convention checks recognize statically provable service types forwarded through source-visible custom lifetime constructors. Arbitrary expressions and unavailable constructor bodies remain unproven.

*   **DI lifetime declarations**: DRNDEPENDENCYINJECTION002 requires one lifetime attribute bound to the matching IName interface on concrete Application/Infra service implementations. Paired interfaces can belong to any recognized role, including SharedKernel, Utils and Contract. Generic and keyed lifetime derivatives count, including inherited attributes when no direct lifetime is declared. Eligible HostedServiceAttribute implementations satisfy IHostedService pairs through their unkeyed hosted-service registration. Self-registration, another service binding or ambiguous lifetime attributes fail this rule even with manual registration or unknown composition. The warning fails warnings-as-errors builds.

*   **Subdomain scope**: Ownership boundaries apply to Domain types and recognized persistence, repository, service and SourceKnown controller roles. Razor page models and unclassified presentation/utility helpers no longer require business-subdomain namespaces. Page models may use domain entities without being treated as public API contracts. MVC API contract checks remain active.

*   **Infrastructure conventions**: Configuration names must end in Config. DRNPERSISTENCE005 checks discovery for each entity binding of a multi-entity configuration. Explicit application covers only the selected entity's mapping. DRNSTRUCTURE006 requires dedicated Repositories, Configurations and Migrations folders/namespaces. DRNSTRUCTURE007 requires context-prefixed custom options names ending in Options before an optional Attribute suffix. Prefer names such as QAContextNpgsqlDbContextOptionsAttribute. DRNPERSISTENCE009 requires concrete, closed DRN contexts to directly apply custom NpgsqlDbContextOptionsAttribute descendants. Inherited options and framework defaults do not satisfy this declaration requirement. Configuration scaffolding emits Config names. Framework default options remain exempt from naming checks.

*   **Aggregate architecture**: DRNSTRUCTURE004, DRNSTRUCTURE005, DRNREPOSITORY003 and DRNREPOSITORY004 require Entity-suffixed aggregates in matching namespace/folder segments, role suffixes for repositories/services/controllers, Domain repository interfaces beside their aggregates in the same namespace and logical folder, and concrete Infra repository implementations for aggregates assigned to its contexts. Assignment evidence includes DbSets, explicit inclusion, and explicitly applied closed generic configurations (per selected `ApplyConfiguration<TEntity>`, including referenced configurations). Entity-bound service contracts include inherited interface methods. Existing public names may require migration. Unassigned aggregates do not require implementations in every Infra project.

*   **Aggregate DbSets**: DRNPERSISTENCE008 requires instance DbSet properties on `DrnContext<TContext>` descendants to expose `AggregateRoot` descendants, including inherited sets. Child entities and keyless rows trigger a warning that can fail warnings-as-errors builds. The separate Identity context hierarchy is excluded.

*   **Context destinations**: DRNSTRUCTURE003 infers conventional repository and migration destinations from namespaces and logical source homes, and validates destinations for bound repositories without DbSets. Generated context partial declarations do not affect the context's logical home. Generated migration and snapshot paths remain validated. Explicit metadata is required for alternate or ambiguous layouts, with reasons for alternatives. Explicit destinations remain validated even without artifacts. Empty contexts need no destination metadata. Repository-pair scaffolding also accepts conventional destinations without a Context item.

*   **Application conventions**: Project-name role tokens now enable convention warnings without custom properties or an application-root identity. Hosted requires at least one direct Application and Infra project reference, Infra requires Domain with corresponding populated subdomains, and Domain requires Contract. Multiple references and shared dependencies are valid regardless of naming prefix. Every reference must obey the allowed direction. SharedKernel is a foundation role. These warnings can fail warnings-as-errors builds. Standard diagnostic severity settings and narrow suppressions govern intentional exceptions. Identity, classification and authorization rules remain independent.

*   **Target framework**: Runtime applications require .NET 11. Analyzer and code-fix assemblies target .NET 10 for IDE compiler-host compatibility and require compatible Roslyn 5.12 APIs.

*   **Source-Known Controller Policies**: DRNAUTHORIZATION001 requires explicit nonblank named policies on exposed SourceKnown controller actions, declared on the controller or action. Bare `[Authorize]`, roles alone and anonymous access do not satisfy the rule. Register policies with ASP.NET Core and retain resource-specific checks in application code. `[NonAction]` disables an endpoint and exempts it; hiding API documentation does not.

*   **Model Capture Analysis**: DRNCLASSIFICATION011 rejects captures that exceed the compiler's model-analysis work limit. Capture a smaller model or explicitly classify sensitive subtrees so configured paths cannot silently escape analysis.

*   **Diagnostic Field Coverage**: Classification requirements include CVV/CVC/PIN-code fields, usernames, first/last names, dates of birth, IBAN, client and IP-address patterns. Annotate matching model properties or explicitly mark reviewed false positives. Browser and compiler checks use the same baseline catalog.

*   **Dictionary Capture Classification**: DRNCLASSIFICATION006 checks statically known keys, including nested initializers and direct writes. Use explicit dictionary classifications or reviewed exact-name comments. Configured analyzer paths cannot be waived. Backend runtime has no dynamic name fallback.

*   **Model Classification Enforcement**: Transitive compiler errors require matching public readable instance properties to declare classification or `[Sensitive(false)]`. This applies even when properties are not logged or use `[IgnoreLog]`. Move matching anonymous properties to annotated classes or records. Custom Microsoft classifications outside the DRN Personal/Sensitive families require a supported classification for compiler verification. Review models and anonymous configuration/schema projections when upgrading. See the [diagnostic guide](../DRN.Framework.Utils/Logging/Diagnostics/README.md) for capture rules and exemptions.

*   **Logging Boundary Enforcement**: DRNCLASSIFICATION008 reports sensitive ILogger placeholders, scope dictionary keys, and direct classified-property captures, including inherited properties implementing classified interfaces on the receiver type. DRNCLASSIFICATION012 rejects direct property captures when the 256 interface-property scan limit prevents a complete classification decision. Remove or redact affected values before logging. Standard ILogger does not apply DRN runtime redaction.

### New Features

*   **SourceKnown development conventions**: Added bounded ownership, placement, context, persistence, composition and architectural diagnostics, plus reference-visible ownership metadata. Dependency checks distinguish direct project references from transitive references and support compiler reference assemblies. Concrete application EF contexts must implement `IDrnContext`, with inherited implementations and narrow justified exceptions supported. Suppression auditing covers assembly and module declarations with case-insensitive scopes. Inherited options naming diagnostics identify the affected derived context. Observed attribute-scan paths check eligible ordinary services and repositories without explicit registration demands. Ordinary MVC controllers and Razor Pages remain supported. Editor actions offer a guarded options constructor, additive entity configuration, selected scoped repository pairs, combined repository/configuration creation, DTO projections and explicit repository-operation forwarding services. The constructor action supports semicolon-bodied partial contexts. DTO projections decline internal numeric `*Id` properties and classification metadata they cannot preserve. Aggregate/context and controller actions create review plans only, pending project-system and business/security decisions. Attribute-only mapping remains valid without a warning. See the convention guide for supported evidence, exceptions and analysis limits.

*   **Source-Known Services**: Added `ISourceKnownService<TEntity>` (exposing read-only `Repository`), `ISourceKnownService<TEntity, out TDto>` (adding covariant `ToDto(TEntity)`), and `ISourceKnownService<TEntity, out TDto, in TCreateRequest>` (adding `CreateEntityAsync`), along with the matching `SourceKnownService` abstract base classes. `SourceKnownService<TEntity, TDto>` supplies a virtual `ToDto(TEntity)` delegating to `entity.ToDto()` with a runtime assignability check.

*   **Pagination Options**: Bind flat first-page options from query strings or JSON bodies and convert them with `PaginationRequest.DefaultWith(options)`.

*   **Logging Name Exemptions**: Adjacent `// DiagnosticNameExemption: ExactName | Nonempty justification` comments acknowledge DRNCLASSIFICATION003/DRNCLASSIFICATION006/DRNCLASSIFICATION008 name matches. They do not waive classified values, configured analyzer paths, declaration requirements, or child checks. DRNCLASSIFICATION009 reports malformed comments and DRNCLASSIFICATION010 warns about unused comments. Comments do not provide runtime redaction.

*   **Project Classification Rules**: Register `data-classification-rules.json` as an EmbeddedResource or AdditionalFiles item to extend compiler classification checks. Rules also inherit from referenced SharedKernel consumers. The legacy filename `diagnostic-rules.json` remains accepted. DRNCLASSIFICATION007 reports invalid, unreadable, oversized, or ambiguous catalogs.

*   **Sensitive Data Classification**: Added inheritable `[Sensitive]` and `[Personal]` attributes and support for application-derived classifications. Use `[Sensitive(false)]` for reviewed property-name false positives. Positive classification overrides opt-outs. Editor fixes offer Personal, Sensitive, false-positive classification, and correction of invalid opt-outs. These attributes do not change business serialization or binding.

### Bug Fixes

*   DRNENTITY009 and DRNENTITY010 now check the converted application ID or entity ID format when a constant passes through a nullable numeric cast.

*   **Repository bindings**: DRNENTITY013 reports an explicit invalid repository type in an initializer once, without a second diagnostic on the inferred `var`.

*   **Packaged analyzer loading**: Duplicate packaged analyzer entries are removed before compilation.

*   **Pagination Refresh and Input**: Refresh results detect another page from an extra fetched row without requiring a known total or caller hint. Null page-size and cursor JSON members now use defaults instead of throwing during deserialization.

*   **Path containment**: `GetPathWithinDirectory()` accepts missing child files and directories while checking existing ancestors for symbolic links.

*   **Entity Attribute Analyzer**: Incomplete primary-constructor base lists no longer cause analyzer exceptions while editing derived entity attributes.

*   **Entity partitions and collisions**: DRNENTITY006 checks the getter implementing `IAppId.AppId` against its published constant when source is available and reports mutable or unproven values. DRNENTITY002 detects collisions across direct and transitive project references built against compatible SharedKernel assemblies. Cross-assembly collision checks skip referenced `System.*`, `Microsoft.*`, and external dependencies of DRN Framework. Entities declared in DRN Framework assemblies remain eligible.

## Version 0.10.0

### Breaking Changes

*   **Entity ID Format Contracts**: GUID operations add `SourceKnownEntityIdFormat` (`ConfiguredDefault = 0`, `Secure = 1`, `Plain = 2`, `Auto = 3`), defaulting to `ConfiguredDefault`. Custom `ISourceKnownEntityIdOperations` implementations must expose an immutable Secure/Plain `DefaultFormat`. Rebuild consumers and update GUID method signatures and method-group bindings. Format is enforced when parsing GUIDs; parsed-record validation takes no format argument and does not reauthenticate GUIDs. Null GUIDs remain supported; undefined formats throw even for null GUIDs and empty GUID batches.
*   **Explicit Expected Identity**: Replace byte-only identity arguments with `new EntityTypeId(entityType, expectedAppId)`: `Generate(long, byte)` becomes `Generate(long, EntityTypeId)`, `HasSameEntityType(byte)` becomes `HasSameEntityTypeId(EntityTypeId)`, and `Validate(byte)` becomes `Validate(EntityTypeId)`. Domain `GetEntityId` accepts this composite identity or a generic entity type. Validation checks both components; `ValidateId()` and the domain GUID helper's boolean option remain validity-only.
*   **Generation Time Policy**: New IDs use a minimum of `2026-09-09T00:00:00Z` and an epoch of `2025-01-01T00:00:00Z` by default. Configure overrides through `SourceKnownGenerationTime.Initialize(minimumUtc, defaultEpoch)` before startup or first ID/epoch use; an explicit epoch requires a minimum. Both values then freeze. Historical reads remain exempt from the floor, and every service and restart using a dataset must retain its origin. See [configuration and limits](README.md#trusted-minimum-generation-time).
*   **Entity Partitions and Analyzers**: Annotate concrete, effectively non-private entities with `[EntityType<TApp>(byte)]` or a supported derived attribute; entity type values must be unique per AppId. Derived attributes must forward one byte or byte-backed enum argument unchanged. Transitive analyzers validate declarations/partitions and require `<AllowMultipleAppIds>true</AllowMultipleAppIds>` for production projects combining partitions. Invalid constant partitions/formats and abstract metadata/repository bindings are errors; duplicate names, hidden identity members, and concrete generic entities produce warnings. Generic forwarding, runtime-instance overloads, and dynamic values remain supported. See [diagnostics and migration requirements](README.md#compile-time-roslyn-analyzers).
*   **Entity Operation Initialization**: `DRN0014` rejects `GetEntityId`, `ToSecure`, and `ToPlain` on provably fresh entities, including aliases and constructor `this`. Use injected ID utilities before initialization. Unknown effects and potentially null inputs remain runtime concerns; initialization does not prove persistence.

### New Features

*   **Application Partitioning**: Added `IAppId`, `EntityTypeAttribute<TApp>`, and `EntityTypeId(entityType, appId)`. Built-in partitions are `DefaultApp` (0), `NexusApp` (126), and `TestApp` (127); `[TestEntityType(byte)]` selects the test partition. `SourceKnownEntity.GetAppId` and `GetEntityTypeId` expose entity metadata.

### Bug Fixes

*   **Pagination**: `PaginationRequest.From()` defaults to page size 10, preserves omitted options, and resets pagination when size, effective maximum size, or sort direction changes. Page jumps preserve direction and are bounded to ten pages without integer underflow.
*   **IgnoredLog Null Handling**: `IgnoredLog(this object? obj)` returns `false` when given `null` input instead of throwing a `NullReferenceException`.

## Version 0.9.8

Version alignment release; no package-specific behavior changes.

## Version 0.9.7

### Breaking Changes

*   **CancellationScopeKey Namespace And Diagnostics**: `CancellationScopeKey` moved from `DRN.Framework.Utils.Cancellation` to `DRN.Framework.SharedKernel.Cancellation`. Its public `OwnerType` and `Name` accessors and custom identity-revealing `ToString()` output were removed; default struct formatting does not expose key identity.
    *   *Migration*: Update imports from `using DRN.Framework.Utils.Cancellation;` to `using DRN.Framework.SharedKernel.Cancellation;`. Treat keys as opaque values created through the `For(...)` factories. If diagnostics require a human-readable label, keep a caller-owned label alongside the key rather than parsing or inspecting the key.
*   **Repository Cancellation Default**: When `RepositorySettings<TEntity>.ScopeKey` is `null`, repositories use the root cancellation scope, so `CancelChanges()` and `CancelWhen(token)` affect root-linked operations.
    *   *Migration*: Set `ScopeKey` to a stable `CancellationScopeKey` when a repository group must remain isolated.

### New Features

*   **CancellationScopeKey Primitive**: `CancellationScopeKey` is now available in `DRN.Framework.SharedKernel.Cancellation`. Its `For(...)` factories create type-owned or ownerless named keys, `IsValid` distinguishes factory-created keys from the invalid default value, and names use ordinal equality and may be empty or whitespace but not `null`.
*   **Repository Cancellation Settings**: `RepositorySettings<TEntity>` now includes `ScopeKey` for configuring child cancellation scopes on repository instances.

### Bug Fixes

*   **Pagination Required-Member Construction**: `PaginationResultInfo` now marks its fully initializing JSON constructor as satisfying inherited required members, allowing direct construction without redundant object initializers.

## Version 0.9.6

### Security

*   **Secure TempPath Directory**: `AppConstants.TempPath` now resolves from `DrnAppDataSettings__TempPath`, `DrnAppDataSettings__DataPath/Temp`, then local app data `Temp`, avoiding predictable shared temp roots (CWE-377/CWE-379).

### New Features

*   **Shared Extension Methods**: Moved casing and safe path helpers to `DRN.Framework.SharedKernel.Extensions`.

### Breaking Changes

*   **Repository Cancellation API**: `ISourceKnownRepository<TEntity>.CancellationToken` is now read-only, and `MergeCancellationTokens(token)` was replaced by `CancelWhen(token)`. Remove direct token assignments and use `CancelWhen(token)` for lifetime cancellation links.
*   **AppConstants TempPath Ownership**: `AppConstants.TempPath` resolves only the temp root. Use `IAppData` for directory creation, cleanup, and child paths.

## Version 0.9.5

### Changed

*   **.NET Version Alignment**: Package release aligned with the DRN.Framework 0.9.5 dependency wave for .NET 10.0.9.

## Version 0.9.4

Dependencies upgraded to dotnet 10.0.8

## Version 0.9.3

Dependencies upgraded to dotnet 10.0.7

## Version 0.9.2

Dependencies upgraded to dotnet 10.0.6

## Version 0.9.1

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and is proud to inherit his spiritual legacy: 'I am not leaving behind any definitive text, any dogma, any frozen, rigid rule as my spiritual legacy. My spiritual wealth is science and reason. Those who wish to embrace me after my death will become my spiritual heirs if they accept the guidance of reason and science on this fundamental axis.'

## Version 0.9.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and stands behind his remarkable words: 'Peace at home, peace in the world.'

### Breaking Changes

*   **`ToUnsecure` → `ToPlain`**: Renamed on `ISourceKnownEntityIdOperations`, `SourceKnownEntity`, and `ISourceKnownRepository<TEntity>`. Semantics unchanged — rename call sites to compile.

## Version 0.8.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals, rooted in his timeless words that 'science is the truest guide in life.' In that spirit, and to honor the 14 March Scientists Day, this release is dedicated to the researchers working for the benefit of humanity, and to the rejection of my first academic paper :) ([JOSS #10176](https://github.com/openjournals/joss-reviews/issues/10176)).

## Version 0.7.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and honors 8 March, International Women's Day, a cause inseparable from his vision of equality. This release is dedicated to freedom of speech, democracy, women's rights, and Prof. Dr. Ümit Özdağ, a defender of Mustafa Kemal Atatürk’s enlightenment ideals.

> [!WARNING]
> Since v0.6.0 (released 10 November 2024), substantial changes have occurred. This release notes file has been reset to reflect the current state of the project as of 08 March 2026. Previous history has been archived to maintain a clean source of truth based on the current codebase.

### New Features

*   **Domain Primitives**
    *   **SourceKnownEntity**: Base class implementing `IHasEntityId`, `IEquatable`, and `IComparable`. Features internal `long Id`, external `Guid EntityId`, and optimistic concurrency (`ModifiedAt`).
    *   **AggregateRoot**: Marker base for DDD roots.
    *   **Domain Events**: `DomainEvent` base with specialized `EntityCreated`, `EntityModified`, and `EntityDeleted` variants.
    *   **Identity System**: `[EntityType(byte)]` attribute for type discrimination. `SourceKnownId` structure for high-performance distributed IDs.
*   **Repository & Data Access**
    *   **ISourceKnownRepository**: Standardized contract for `GetAsync`, `GetOrDefaultAsync`, `GetEntityId` (validation), and batch operations.
    *   **Advanced Pagination**: `PaginationRequest` with `PageCursor` (FirstId/LastId) for stable bidirectional navigation (`Next`, `Previous`, `Refresh`).
    *   **Filtering**: `EntityCreatedFilter` for date-range queries.
*   **Exception System**
    *   **Hierarchy**: `DrnException` based types mapping to HTTP status codes.
    *   **Factory Methods**: `ExceptionFor.NotFound`, `Validation`, `Unauthorized`, `Forbidden`, `Conflict`, `Expired`, `UnprocessableEntity`, `Configuration`, `MaliciousRequest`.
    *   **Categorization**: Support for exception `Category` and `Status` properties.
*   **JSON Conventions**
    *   **Web Defaults**: `JsonSerializerDefaults.Web` active by default.
    *   **Enhancements**: `JsonStringEnumConverter`, `CamelCase`, `AllowTrailingCommas`, `NumberHandling.AllowReadingFromString`, and `Int64` string conversion.
*   **Core Utilities & Constants**
    *   **AppConstants**: Global access to `ProcessId`, `AppInstanceId`, `EntryAssemblyName`, `TempPath`, and `LocalIpAddress`.
    *   **Security Attributes**: `[SecureKey]` for string validation and `[IgnoreLog]` to prevent leaking sensitive data in logs.
*   **Entity ID Operations**
    *   **ISourceKnownEntityIdOperations Interface**: Extracted core entity ID operations (`Generate`, `Parse`, `ToSecure`, `ToUnsecure`) into a SharedKernel interface, replacing internal `Func` delegate fields with a single typed contract. `ISourceKnownEntityIdUtils` in Utils inherits this interface.
    *   **Entity Secure Conversion**: `SourceKnownEntity` now exposes `ToSecure` / `ToUnsecure` methods for idempotent conversion between encrypted and plaintext `SourceKnownEntityId` forms.
    *   **Repository Secure Conversion**: `ISourceKnownRepository<TEntity>` now exposes `ToSecure` / `ToUnsecure` methods for converting entity IDs at the repository level.

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/.agent/rules/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
