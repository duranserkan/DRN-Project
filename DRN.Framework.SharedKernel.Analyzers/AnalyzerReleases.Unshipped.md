; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

| Rule ID | Category   | Severity | Notes                                                                                                                     |
|---------|------------|----------|---------------------------------------------------------------------------------------------------------------------------|
| DRNARCHITECTURE001 | DRN.Conventions.Configuration | Warning | Public contract inspection exceeded its analysis limit |
| DRNARCHITECTURE002 | DRN.Conventions.Contract | Warning | Keep public contracts free of persistence types |
| DRNARCHITECTURE003 | DRN.Conventions.Architecture | Warning | Respect declared direct role references |
| DRNARCHITECTURE004 | DRN.Conventions.Architecture | Warning | Place semantic implementations in their owning role |
| DRNARCHITECTURE005 | DRN.Conventions.Architecture | Warning | Remove forbidden implementation dependencies from the consuming layer |
| DRNARCHITECTURE006 | DRN.Conventions.Architecture | Warning | Require direct application-layer project references |
| DRNAUTHORIZATION001 | DRN.Security | Error | Source-Known controller actions require explicit named policies and cannot permit anonymous access |
| DRNCLASSIFICATION001 | DRN.Classification | Error | Matching model property requires classification or explicit name exemption |
| DRNCLASSIFICATION002 | DRN.Classification | Error | Invalid Sensitive(false) target or derived false construction |
| DRNCLASSIFICATION003 | DRN.Classification | Error | Diagnostic path or prefix requires an explicit capture decision |
| DRNCLASSIFICATION004 | DRN.Classification | Error | Custom Microsoft classification outside supported DRN families cannot be verified |
| DRNCLASSIFICATION005 | DRN.Classification | Error | Matching anonymous properties require a named class or record with explicit classification or false-positive exemption |
| DRNCLASSIFICATION006 | DRN.Classification | Error | Matching dictionary captures require AddDictionary or SanitizeDictionary with key classifications |
| DRNCLASSIFICATION007 | DRN.Classification | Error | Project or inherited classification catalog is unreadable, invalid, oversized, or ambiguous |
| DRNCLASSIFICATION008 | DRN.Classification | Error | Sensitive ILogger placeholders, scope dictionary keys, or directly captured classified properties require removal or explicit redaction |
| DRNCLASSIFICATION009 | DRN.Classification | Error | DiagnosticNameExemption comment requires an exact name and nonempty justification within documented bounds |
| DRNCLASSIFICATION010 | DRN.Classification | Warning | DiagnosticNameExemption comment suppresses no name diagnostic |
| DRNCLASSIFICATION011 | DRN.Classification | Error | Model capture exceeds the analysis work limit and may leave configured paths unchecked |
| DRNCLASSIFICATION012 | DRN.Classification | Error | Logging capture exceeds the interface-property analysis limit and may leave classifications unchecked |
| DRNCONVENTIONEXCEPTION001 | DRN.Conventions.Exceptions | Warning | Keep observable exceptions narrow and reasoned |
| DRNCONVENTIONINPUT001 | DRN.Conventions.Configuration | Warning | Invalid or incomplete application convention input |
| DRNDEPENDENCYINJECTION001 | DRN.Conventions.Registration | Warning | Establish effective registration for explicit demands and eligible services on observed attribute-scan paths |
| DRNDEPENDENCYINJECTION002 | DRN.Conventions.Registration | Warning | Require one lifetime attribute bound to the matching IName interface on concrete Application/Infra implementations |
| DRNDEPENDENCYINJECTION003 | DRN.Conventions.Registration | Warning | Require Hosted, Application, Infra and Utils project modules to scan their own assembly for attributed services |
| DRNDTO001 | DRN.Conventions.Contract | Warning | Require entity-owned DTO projection |
| DRNDTO002 | DRN.Conventions.Structure | Warning | Require DTO subdomain and entity folders |
| DRNDTO003 | DRN.Conventions.Contract | Warning | Preserve projected property classifications |
| DRNDTO004 | DRN.Conventions.Structure | Warning | Place entity-prefixed requests and responses beside their DTO |
| DRNDTO005 | DRN.Conventions.Contract | Warning | Do not expose DTOs through public requests, responses, or controller endpoints |
| DRNDTO006 | DRN.Conventions.Contract | Warning | Use explicit Contract Request and Response types on public endpoints |
| DRNDTO007 | DRN.Conventions.Contract | Warning | Align inherited SourceKnownService DTO projection with entity ToDto() return type |
| DRNENTITY001 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRNENTITY002 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRNENTITY003 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRNENTITY004 | DRN.Domain | Warning  | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRNENTITY005 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRNENTITY006 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRNENTITY007 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRNENTITY008 | DRN.Domain | Error    | Entities require a derived attribute forwarding one byte-backed enum parameter |
| DRNENTITY009 | DRN.Domain | Error    | Constant EntityTypeId AppId must be within 0..127 |
| DRNENTITY010 | DRN.Domain | Error    | Constant SourceKnownEntityIdFormat arguments and assignments must be defined |
| DRNENTITY011 | DRN.Domain | Warning  | Entity descendants must not hide framework identity properties |
| DRNENTITY012 | DRN.Domain | Warning  | Concrete generic entities share identity across constructed types |
| DRNENTITY013 | DRN.Domain | Error    | Use a concrete entity type for Source-Known repository bindings and metadata APIs |
| DRNENTITY014 | DRN.Domain | Error    | Entity ID operations are not initialized on a provably fresh receiver |
| DRNENUM001 | DRN.Conventions.Contract | Warning | Declare application enums in Contract or mark Domain-only enums with [DomainOnly] |
| DRNENUM002 | DRN.Conventions.Contract | Warning | Restrict [DomainOnly] to enums declared in Domain projects |
| DRNENUM003 | DRN.Conventions.Contract | Warning | Do not expose [DomainOnly] enums through public contracts or endpoints |
| DRNENUM004 | DRN.Conventions.Contract | Warning | Require [AllowZero] on Contract or Domain enums that define a zero member |
| DRNENUM005 | DRN.Conventions.Contract | Warning | Require a named zero member on enums marked with [AllowZero] |
| DRNOWNERSHIP001 | DRN.Conventions.Ownership | Warning | Declare unambiguous subdomain ownership |
| DRNOWNERSHIP002 | DRN.Conventions.Ownership | Warning | Justify cross-subdomain entity use |
| DRNOWNERSHIP003 | DRN.Conventions.Architecture | Warning | Require populated Domain subdomains for infrastructure |
| DRNOWNERSHIP004 | DRN.Conventions.Ownership | Warning | Require at least one populated subdomain folder in every Domain project |
| DRNPERSISTENCE001 | DRN.Conventions.Context | Warning | Provide supported context construction |
| DRNPERSISTENCE002 | DRN.Conventions.Context | Warning | Establish effective Npgsql setup on the selected path |
| DRNPERSISTENCE003 | DRN.Conventions.Context | Warning | Preserve required base model configuration |
| DRNPERSISTENCE004 | DRN.Conventions.Persistence | Warning | Resolve inconsistent persistence membership |
| DRNPERSISTENCE005 | DRN.Conventions.Persistence | Warning | Make declared configuration discoverable |
| DRNPERSISTENCE006 | DRN.Conventions.Migrations | Warning | Align migration context identity and destination |
| DRNPERSISTENCE007 | DRN.Conventions.Context | Warning | Require IDrnContext for concrete application-owned DbContext implementations |
| DRNPERSISTENCE008 | DRN.Conventions.Persistence | Warning | Require AggregateRoot entities for DrnContext DbSet properties |
| DRNPERSISTENCE009 | DRN.Conventions.Context | Warning | Require concrete DRN contexts to directly declare a custom NpgsqlDbContextOptionsAttribute descendant |
| DRNREPOSITORY001 | DRN.Conventions.Repository | Warning | Align repository aggregate and context |
| DRNREPOSITORY002 | DRN.Conventions.Application | Warning | Align service repository binding |
| DRNREPOSITORY003 | DRN.Conventions.Repository | Warning | Require an aggregate repository interface in the same Domain namespace and logical folder |
| DRNREPOSITORY004 | DRN.Conventions.Repository | Warning | Require Infra repository implementations for assigned aggregates |
| DRNSTRUCTURE001 | DRN.Conventions.Structure | Warning | Align namespace and logical placement |
| DRNSTRUCTURE002 | DRN.Conventions.Context | Warning | Context name requires a nonempty Context stem |
| DRNSTRUCTURE003 | DRN.Conventions.Structure | Warning | Infer conventional context destinations and require explicit alternate or ambiguous destinations |
| DRNSTRUCTURE004 | DRN.Conventions.Structure | Warning | Require semantic Entity, Repository, Service, Controller and Config suffixes |
| DRNSTRUCTURE005 | DRN.Conventions.Structure | Warning | Match aggregate namespace and directory to its name without Entity |
| DRNSTRUCTURE006 | DRN.Conventions.Structure | Warning | Require dedicated Repositories, Configurations and Migrations folders |
| DRNSTRUCTURE007 | DRN.Conventions.Context | Warning | Require context-prefixed custom options names ending in Options |
| DRNSTRUCTURE008 | DRN.Conventions.Structure | Warning | Keep Domain grouping folders folders-only and restrict _Shared to subdomain value objects |
