; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

| Rule ID | Category   | Severity | Notes                                                                                                                     |
|---------|------------|----------|---------------------------------------------------------------------------------------------------------------------------|
| DRN0001 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRN0002 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRN0003 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRN0004 | DRN.Domain | Warning  | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRN0005 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRN0006 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRN0007 | DRN.Domain | Error    | https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/README.md#compile-time-roslyn-analyzers |
| DRN0008 | DRN.Domain | Error    | Derived entity attribute constructor must preserve one byte or byte-backed enum parameter |
| DRN0009 | DRN.Domain | Error    | Constant EntityTypeId AppId must be within 0..127 |
| DRN0010 | DRN.Domain | Error    | Constant SourceKnownEntityIdFormat arguments and assignments must be defined |
| DRN0011 | DRN.Domain | Warning  | Entity descendants must not hide framework identity properties |
| DRN0012 | DRN.Domain | Warning  | Concrete generic entities share identity across constructed types |
| DRN0013 | DRN.Domain | Error    | Concrete entity type required for Source-Known repository bindings and metadata APIs |
| DRN0014 | DRN.Domain | Error    | Entity ID operations are not initialized on a provably fresh receiver |
| DRN0015 | DRN.Classification | Error | Matching model property requires classification or explicit name exemption |
| DRN0016 | DRN.Classification | Error | Invalid Sensitive(false) target or derived false construction |
| DRN0017 | DRN.Classification | Error | Diagnostic path or prefix requires an explicit capture decision |
| DRN0018 | DRN.Classification | Error | Custom Microsoft classification outside supported DRN families cannot be verified |
| DRN0019 | DRN.Classification | Error | Matching anonymous properties require a named class or record with explicit classification or false-positive exemption |
| DRN0020 | DRN.Classification | Error | Matching dictionary captures require AddDictionary or SanitizeDictionary with key classifications |
| DRN0021 | DRN.Classification | Error | Project or inherited classification catalog is unreadable, invalid, oversized, or ambiguous |
| DRN0022 | DRN.Classification | Error | Sensitive ILogger placeholders, scope dictionary keys, or directly captured classified properties require removal or explicit redaction |
| DRN0023 | DRN.Classification | Error | DiagnosticNameExemption comment requires an exact name and nonempty justification within documented bounds |
| DRN0024 | DRN.Classification | Warning | DiagnosticNameExemption comment suppresses no name diagnostic |
| DRN0025 | DRN.Classification | Error | Model capture exceeds the analysis work limit and may leave configured paths unchecked |
| DRN0026 | DRN.Security | Error | Source-Known controller actions require explicit named policies and cannot permit anonymous access |
| DRN0027 | DRN.Classification | Error | Logging capture exceeds the interface-property analysis limit and may leave classifications unchecked |
| DRNCV001 | DRN.Conventions.Configuration | Warning | Invalid or incomplete application convention input |
| DRNCV002 | DRN.Conventions.Ownership | Warning | Declare unambiguous subdomain ownership |
| DRNCV003 | DRN.Conventions.Structure | Warning | Align namespace and logical placement |
| DRNCV004 | DRN.Conventions.Context | Warning | Context name requires a nonempty Context stem |
| DRNCV005 | DRN.Conventions.Context | Warning | Provide supported context construction |
| DRNCV006 | DRN.Conventions.Context | Warning | Establish effective Npgsql setup on the selected path |
| DRNCV007 | DRN.Conventions.Context | Warning | Preserve required base model configuration |
| DRNCV008 | DRN.Conventions.Persistence | Warning | Resolve inconsistent persistence membership |
| DRNCV009 | DRN.Conventions.Persistence | Warning | Make declared configuration discoverable |
| DRNCV010 | DRN.Conventions.Structure | Warning | Infer conventional context destinations and require explicit alternate or ambiguous destinations |
| DRNCV011 | DRN.Conventions.Repository | Warning | Align repository aggregate and context |
| DRNCV012 | DRN.Conventions.Registration | Warning | Establish effective registration for explicit demands and eligible services on observed attribute-scan paths |
| DRNCV013 | DRN.Conventions.Application | Warning | Align service repository binding |
| DRNCV014 | DRN.Conventions.Contract | Warning | Keep public contracts free of persistence types |
| DRNCV015 | DRN.Conventions.Architecture | Warning | Respect declared direct role references |
| DRNCV016 | DRN.Conventions.Architecture | Warning | Place semantic implementations in their owning role |
| DRNCV017 | DRN.Conventions.Migrations | Warning | Align migration context identity and destination |
| DRNCV018 | DRN.Conventions.Exceptions | Warning | Keep observable exceptions narrow and reasoned |
| DRNCV019 | DRN.Conventions.Ownership | Warning | Justify cross-subdomain entity use |
| DRNCV020 | DRN.Conventions.Context | Warning | Require IDrnContext for concrete application-owned DbContext implementations |
| DRNCV021 | DRN.Conventions.Architecture | Warning | Require direct application-layer project references |
| DRNCV022 | DRN.Conventions.Architecture | Warning | Require populated Domain subdomains for infrastructure |
| DRNCV023 | DRN.Conventions.Persistence | Warning | Require AggregateRoot entities for DrnContext DbSet properties |
| DRNCV024 | DRN.Conventions.Structure | Warning | Require semantic Entity, Repository, Service, Controller and Config suffixes |
| DRNCV025 | DRN.Conventions.Structure | Warning | Match aggregate namespace and directory to its name without Entity |
| DRNCV026 | DRN.Conventions.Repository | Warning | Require an aggregate repository interface in the same Domain namespace and logical folder |
| DRNCV027 | DRN.Conventions.Repository | Warning | Require Infra repository implementations for assigned aggregates |
| DRNCV028 | DRN.Conventions.Structure | Warning | Require dedicated Repositories, Configurations and Migrations folders |
| DRNCV029 | DRN.Conventions.Context | Warning | Require context-prefixed custom options names ending in Options |
| DRNCV030 | DRN.Conventions.Context | Warning | Require concrete DRN contexts to directly declare a custom NpgsqlDbContextOptionsAttribute descendant |
| DRNCV031 | DRN.Conventions.Registration | Warning | Require one lifetime attribute bound to the matching IName interface on concrete Application/Infra implementations |
| DRNCV032 | DRN.Conventions.Registration | Warning | Require Hosted, Application, Infra and Utils project modules to scan their own assembly for attributed services |
