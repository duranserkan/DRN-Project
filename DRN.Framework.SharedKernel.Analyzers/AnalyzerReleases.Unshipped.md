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
