# Diagnostic redaction and grouping

Use `IScopedLog` for private diagnostic context. Scoped logs capture bounded, detached values once. `GetLogs()`, `LogScoped()`, and `ToString()` redact explicitly classified values. Sensitive values remain in scope memory until the scope is released. [Scope events](../../README.md#scope-events) retain their contents without redaction.

Runtime capture does not match property names, keys, configured paths, URL segments, query names, or form names. Backend name matching belongs to compiler analysis. Explicit `[Sensitive]`, `[Personal]`, other non-None Microsoft classifications, dictionary classifications, and `[IgnoreLog]` remain effective at runtime.

Classification and redaction of dynamic data are the consuming application's responsibility, not the framework's. Decide what is sensitive for your use case and classify, redact, or omit it before capture or export. This includes request values, external payloads, dynamic dictionary entries, and free text.

In Development, `GetLogs(redact: false)` returns an unredacted view. Other environments reject this flag. Development exception pages use this view while retaining bounds, ignored members, buffering rules, and HTML encoding. Direct sanitizer calls redact immediately. Custom implementations and already-redacted values cannot recover removed data. Configuration debug-view protection remains separate. Keep Development pages and startup reports private.

## Declare sensitive data

```csharp
public sealed class VerifyRequest
{
    [Sensitive] public string Code { get; init; } = string.Empty;
    [Personal] public string Email { get; init; } = string.Empty;
    [Sensitive(false, Justification = "Recovery availability flag.")]
    public bool HasRecoveryCodes { get; init; }
}
```

Import `DRN.Framework.SharedKernel.Attributes`. Apply classifications to a type or captured property. Runtime redaction emits `[redacted]`. Public fields are omitted, including annotated fields. Scoped capture evaluates classified getters once and retains classification metadata. Reads do not reexecute getters. Direct sanitization skips classified getters. Both paths skip `[IgnoreLog]` getters. Passing `model.Password` directly evaluates the getter before capture and loses its property metadata.

Positive classifications on inherited members, mapped interfaces, declared types, and runtime value types remain authoritative. An unrelated explicit interface implementation does not classify a same-named public property. `AddDrnUtils` registers `DiagnosticRedactor` for Sensitive, Personal, and their combined classification set through `IRedactorProvider`.

### Compile-time classification

SharedKernel distributes these diagnostics transitively. Public readable instance non-indexer properties are checked independently of logging use, including interfaces, generics, records, and nonpublic containing types. Static properties, private getters, write-only properties, and fields are excluded. Generated sources are analyzed without reporting diagnostics in generated code. `[IgnoreLog]` does not suppress declaration analysis.

| Diagnostic | Meaning |
|---|---|
| DRN0015 | A matching property requires classification or direct Sensitive(false). |
| DRN0016 | Invalid Sensitive(false) target or a derived attribute forwarding false. |
| DRN0017 | A matching scalar/untyped capture path or external property requires a decision. |
| DRN0018 | An unrelated Microsoft classification cannot be verified statically. |
| DRN0019 | A matching anonymous property requires an annotated named class or record. |
| DRN0020 | A statically known matching dictionary key requires classification or a reviewed comment. |
| DRN0021 | Invalid, unreadable, oversized, or ambiguous classification catalog. |
| DRN0022 | Sensitive ILogger names or directly captured classified properties require a decision. |
| DRN0023 | Malformed DiagnosticNameExemption comment. |
| DRN0024 | Warning: a valid DiagnosticNameExemption comment suppresses no name diagnostic. |
| DRN0025 | Model capture exceeded the analysis work limit, so configured paths may remain unchecked. Capture a smaller model or classify sensitive subtrees explicitly. |

Company attributes may derive from Sensitive or Personal, including in referenced assemblies. Derived Sensitive attributes must remain positive. Runtime non-None Microsoft classifications still redact, but unrelated classification families do not satisfy compiler verification. A custom attribute returning None is not a supported exemption.

Code fixes offer Personal and Sensitive with selected Fix All. Sensitive(false) and DRN0016's Use Sensitive(true) require individual selection. Generated/external declarations receive no direct-edit fix. Editor hosts must support the packaged .NET 11/Roslyn 5.9 analyzer and code-fix assemblies.

### Property false positives

`[Sensitive(false)]` records a reviewed property-name exemption with optional Justification. It is invalid on types and fields. Positive classifications still win. An inherited unchanged property retains its exemption; overrides and hidden properties need their own decision. Interface exemptions do not waive concrete declaration checks. `[IgnoreLog, Sensitive(false)]` remains ignored during capture.

No unused-exemption warning is issued for attributes, including `[Sensitive(false)] int Count`, or reason/policy APIs. DRN0024 applies only to comments.

### Typed models and untyped entries

The analyzer checks named properties independently of built-in logging-prefix matches. Anonymous declarations are checked independently, including nested and inferred members. Matching anonymous properties must become named annotated models. Source-property annotations do not transfer to anonymous projections. Reason strings and capture policies do not waive declaration requirements.

Statically known dictionary keys and scalar capture paths are checked at supported calls. Dynamic names and unexamined paths have no runtime fallback. Typed and untyped capture both use explicit runtime classifications without rescanning names.

Reason overloads such as `log.Add("Response_StatusCode", 503, exemptionReason: "HTTP status.")` acknowledge compiler name checks. Reasons must be nonblank, bounded, and application-authored. Supported values are null, nullable scalars, strings, characters, booleans, numeric primitives, enums, Guid, date/time values, TimeSpan, and Uri. Complex values and invalid reasons return an unavailable marker without traversal. Non-finite numbers remain unavailable. Uri values use structural URL processing regardless of their key. Import `DRN.Framework.Utils.Logging.Diagnostics` for interface extension overloads.

### Dictionary classifications

```csharp
log.AddDictionary("Response", values,
    new("Password", DiagnosticDataKind.Sensitive),
    new("Email", DiagnosticDataKind.Personal),
    new("StatusCode", DiagnosticDataKind.NonSensitive, "HTTP status."));
```

`SanitizeDictionary` provides the same classification contract without scoped logging. Both accept `IReadOnlyDictionary<string, TValue>` and `params DiagnosticKeyClassification[]`. Exact, case-sensitive keys apply only at that dictionary level. Sensitive/Personal redact the whole value. Positive classifications win over NonSensitive entries. NonSensitive requires a bounded nonblank reason and a supported scalar. Invalid classifications fail closed. Foreign implementations receive an unavailable marker because they cannot preserve this contract.

Unclassified keys are visible at runtime. Nested dictionaries need their own explicit classifications for sensitive values. Direct sanitization skips classified traversal; scoped capture retains values for Development-only raw reads.

Configured analyzer paths also apply to AddDictionary and SanitizeDictionary. Inline classification constructors with constant, exact keys and Sensitive or Personal kinds prove protection for the whole entry, including descendants. NonSensitive entries and name comments cannot waive configured paths. Unknown classification expressions remain checked. Known null, empty and exact `[redacted]` dictionary values require no name diagnostic, including in ILogger scopes.

### Exact capture policies

`DiagnosticCapturePolicy` retains immutable, bounded `DiagnosticNameExemption` entries for API compatibility. Kinds, original segments and reasons are validated, but runtime capture does not match them. The analyzer recognizes reason/policy overloads as name acknowledgements without interpreting individual policy entries. Configured analyzer paths and declaration checks remain enforced. Prefer adjacent comments when an exact capture-name acknowledgement is needed. Policies never override positive runtime classifications.

Native copies preserve explicit classification metadata and detached values while enforcing destination budgets. They do not reclassify names. Public snapshots carry no classification metadata. Re-adding a raw public snapshot cannot restore its original annotations. Copies to custom sanitizers expose only the source's redacted view.

Metric APIs use code-owned identifiers without native runtime name matching. Custom sanitizers retain their own sensitivity checks. Do not use request values as metric identifiers.

### Boundaries and migration

DRN0022 covers Microsoft LoggerExtensions templates, template BeginScope, scope dictionary initializers/direct local writes, and directly extracted classified property values in ILogger and DRN captures. Escaped braces are literals; format/alignment suffixes are excluded. Known null, empty, and exact `[redacted]` values are allowed. DRN0017 also checks AddIfNotNullOrEmpty, excluding known null and empty values. Standard ILogger does not apply DRN runtime redaction.

Direct property checks include inherited implementations of classified interfaces on the receiver's statically known type. Unrelated explicit interface implementations do not classify a same-named public property.

```csharp
// DiagnosticNameExemption: TokenCount | Reviewed numeric metric.
logger.LogInformation("Processed {TokenCount} tokens", count);

// DiagnosticNameExemption: TokenCount | Reviewed numeric metric.
log.Add("TokenCount", count);
```

Use one exact, case-sensitive name per comment immediately above the statement, without a blank line. Names must be 1-512 characters, justifications nonempty, and comments at most 2048 characters. Wildcards are unsupported. Expression statements, local declarations (including using var), using statements, and return statements are supported. Comments do not extend into lambdas or later statements.

DRN0017 comments match the reported capture path, including external-property parents and generated `_Seconds` suffixes. DRN0020 comments match exact dictionary keys at the capture statement, not at earlier declarations/writes. Exempting a parent never exempts other child keys. Comments cannot waive classified values, configured analyzer sensitiveFieldPaths, or declaration requirements. Unclassified exempted values remain visible at runtime.

DRN0023 reports malformed comments. DRN0024 warns on valid comments that suppress nothing, including mismatched, detached, redundant, and duplicate comments. Tracking is isolated per compilation. Attributes and reason/policy APIs do not receive this unused-exemption warning.

Direct NSubstitute verification chains are excluded from capture checks. Ordinary substitute calls and model declarations remain checked. Analyzer exploration is bounded and does not follow arbitrary wrappers, aliases, method returns, dynamic keys, LoggerMessage generators/delegates, or custom generic ILogger state. Template analysis bounds are 65,536 characters, 256 visible placeholders/arguments, and 256 interface properties. Dynamic and unexamined values require application-owned classification.

DRN0019 exempts EF column-definition callbacks and supported column selectors by resolved symbols. Migration directories are not excluded. Ordinary anonymous objects inside migrations remain checked.

Suppression of a compiler diagnostic does not classify a value at runtime. Review matching models and captures when upgrading. Do not mass-apply Sensitive or disable all test analysis. Classify generated properties through generator inputs or supported partial declarations where needed.

## Project classification rules

The browser and analyzer consume the same [shared catalog](data-classification-rules.json), using ASCII case/separator normalization and substring matching. It includes secret, token, client, cred, conn, code, key, cvv, cvc, pincode, email, phone, ipadd, username, firstname, lastname, dateofbirth, birthdate, and iban. Broad name, address, pin, pan, and otp patterns are excluded. Classify sensitive OTP values explicitly. Reviewed client IDs and request IP addresses need compiler acknowledgements and remain visible in unclassified backend captures.

Register one project-owned `data-classification-rules.json` as EmbeddedResource or AdditionalFiles:

```json
{
  "secretFields": ["challengeResponse"],
  "personalFields": ["membershipNumber"],
  "sensitiveFieldPaths": ["payload.privateValue"],
  "secretPathSegments": ["handoff"]
}
```

Build targets forward embedded catalogs to analysis and publish metadata retained in reference assemblies. Analyzers inherit additive catalogs from referenced SharedKernel consumers without loading dependency code. Unrelated assemblies are excluded. Identical physical items are deduplicated; distinct own files are rejected. The legacy diagnostic-rules.json filename remains accepted. Rebuild dependencies to publish changed metadata.

Project secret/personal fields extend analyzer names. Project sensitiveFieldPaths enforce full statically known capture paths, including nested scope dictionaries and reason/policy calls. Name exemptions cannot waive them. Runtime does not match any of these groups, including URL segments.

Legacy `DiagnosticLogging` settings `SensitiveFields`, `SensitiveFieldPaths`, and `SensitivePathSegments` remain validated but have no redaction effect. `RuleFile` and `RuleAssemblyName` still load and validate catalogs for compatibility. They do not apply name matching. Relative `RuleFile` paths resolve from `AppContext.BaseDirectory`. Validation occurs once per options instance. Missing configured roots, invalid catalogs, and assembly load failures still fail construction. Missing transitive assemblies are skipped. No directory discovery or live reload occurs.

If retaining runtime catalog loading, deploy loose files through None Update with build/publish copying, or None Include with Link for external files. Embedded catalogs require no loose deployment. RuleFile and RuleAssemblyName merge additively. Analyzer registration alone does not require runtime loading.

The four case-sensitive groups above must be string arrays. Unknown/duplicate groups, nulls, empty normalized names, names over 512 characters, more than 256 rules per file, and files over 65,536 characters are rejected. Inheritance is bounded to 1,024 assemblies, 64 catalogs, and 4,096 rules. Use UTF-8. Catalogs contain names, never secret values. Invalid compiler catalogs report DRN0021 while baseline analysis remains active.

The browser imports the shared catalog at bundle time and supports configured name matching. Server settings do not automatically reach the browser. Configure browser rules separately for project-specific names. Browser input carries no server classification authority.

## Supported capture and limits

Capture supports text, explicit URLs, UTF-8 text/form bodies, primitives, dictionaries, lists, scope events, and DTO projections. It captures readable public instance properties, omits fields/indexers, catches getter failures, detects cycles, and replaces unsupported lazy collections. It does not traverse logical JSON properties or fall back to arbitrary ToString calls. Application getters cannot be preempted.

Explicit SanitizeUrl and Uri capture remove URL credentials, validate encoding, and enforce budgets. Query/form pairs and named fragments retain decoded values without matching names. Bare fragments remain unchanged. Ordinary strings, including fields ending in Url, Uri, or QueryString, remain text. Content scanning is not performed. Exception messages, stacks, and text bodies retain contents within bounds. Unsupported bodies, including JSON, produce the unsupported-body marker. No new request-body capture is enabled.

Browser arrays retain at most 256 positions plus a limit marker. Sparse holes become null; own descriptors are inspected without invoking getters or inherited properties.

| Budget | Default |
|---|---:|
| One raw field | 65,536 UTF-8 bytes |
| Cumulative examined input per scope | 262,144 bytes |
| Retained scoped snapshot | 131,072 bytes |
| One emitted `@Logs` payload | 32,768 serialized UTF-8 bytes |
| Records per emission, including parent | 8 |
| Object depth / projected nodes / entries per collection | 8 / 1,024 / 256 |
| Key/path | 256 UTF-8 bytes |
| Active sanitization work | 50 ms per scope |

`DiagnosticLogOptions` owns validated configuration ranges. Work checks run between sanitizer operations and cannot preempt user getters. Repeated diagnostic additions share capture budgets. Oversized or incomplete raw fields become reason markers before clipping. Snapshot reads use a separate bounded read budget without consuming the capture budget again. Both diagnostic views retain output limits. `DiagnosticCaptureWarnings`, `DiagnosticScopeLimit`, and `DiagnosticCaptureLimited` identify diagnostic capture loss. Output limits include JSON escaping and record metadata, but exclude provider-specific envelopes and scope events.

Scope events have a separate limit of one primary and eight additional events. Further events are omitted without throwing, with `OmittedEventCount` included only when nonzero. See [scope events](../../README.md#scope-events) for capture and copy behavior.

HTTP error-body capture reads at most the field limit plus one byte, restores seekable positions, and does not dispose the response. Unsupported encodings and failed reads produce markers.

## Emission and reconstruction

Call `logger.LogScoped(log)` or `logger.LogScoped(log, LogLevel.Warning)` for explicit severity. Disabled levels skip snapshotting and formatting. Diagnostics from custom `IScopedLog` implementations are sanitized at emission. Sink failures are contained without retries and can lose an entire emission.

A fitting diagnostic payload remains one structured `@Logs` record. Larger diagnostic snapshots use one parent and bounded child records. Retained events and any `OmittedEventCount` appear only on the parent, outside the reconstructed diagnostic payload. Event contents can make that record exceed configured byte limits. Every record contains:

- `DiagnosticGroupId`, a new ID for this emission.
- `LogRecordId` and `ParentLogRecordId`, formatted as `group/index`.
- `PartIndex` and `PartCount`, counting the parent at index zero.
- `RelatedLogRecordIds`, listing the parent and all expected children.
- Applicable `CorrelationId`, real `TraceId`, `TraceIdentifier`, and `ClientError.ClientErrorId`.

The event category `EventId` is independent of these record references. Each child has `PayloadEncoding: "json-text"`, a zero-based `PayloadFragmentIndex`, and `PayloadFragmentCount` excluding the parent. Concatenate `PayloadFragment` strings in index order to reconstruct the bounded sanitized JSON. Explicit redaction happens before fragmentation. Total-limit loss remains explicit in the reconstructed snapshot. Very large context fields may become limit markers.

Delivery is best effort and not atomic. Compare received record IDs with `RelatedLogRecordIds` to detect missing delivery. `OmittedEventCount` counts events excluded by the scope limit, not records lost during delivery. `ScopedLog.ToString()` returns one JSON snapshot containing diagnostics and retained events. Providers may impose their own record limits.

## Protection boundary

Scoped logs, Hosting request diagnostics, Flurl diagnostics, startup reports, developer-page copies, and browser reports retain bounded capture. Explicit classifications protect server models. Backend runtime does not infer classifications from names. Scope events bypass sanitization, including custom sanitizers. Keep them safe to log.

Original exceptions and HttpContext remain available to filters and DiagnosticSource subscribers, which own their export policies. Arbitrary direct logger calls, historical logs, and unclassified sensitive values are not automatically protected.
