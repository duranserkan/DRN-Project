# Diagnostic redaction and grouping

Use `IScopedLog` for private diagnostic context. Scoped logs capture bounded, detached diagnostic values once. `GetLogs()` redacts diagnostics by default; `LogScoped()` and `ToString()` use that redacted view. [Scope events](../../README.md#scope-events) retain their contents without redaction. Sensitive diagnostic values remain in scope memory until the scope is released. Keep public notifications and Problem Details safe independently of diagnostic retention.

In Development, `ScopedLog.GetLogs(redact: false)` returns an unredacted view of the same capture. Other environments reject this flag. Development exception pages opt out for logs and request details while retaining capture limits, `[IgnoreLog]`, buffering rules, and HTML encoding. Direct sanitizer calls still redact immediately. Custom capture implementations and previously sanitized values cannot recover removed data. Configuration debug-view protections remain separate. Keep Development pages and saved startup reports private.

Server and browser URL capture uses `redacted` for protected path segments, query values, and named fragment values, for example `/token/redacted?code=redacted#token=redacted`. Bare fragments such as `#installation` are preserved without content inspection. Captured `QueryString` fields use the same marker. Ordinary diagnostic values, whole-field redaction, and form-body values retain `[redacted]`. URL credentials are removed rather than replaced.

## Declare sensitive data

Dedicated `Measure`, `Increase`, and `IncreaseTimeSpentOn` methods use code-owned metric identifiers. The native sanitizer exempts these identifiers from built-in name rules, while configured rules still check the prefix plus operation name. Custom sanitizers retain their existing sensitivity checks. Do not use request values as metric identifiers.

Native `CopyFrom` preserves metric exemptions and accumulated values while checking destination rules against both the original operation path and the stored field name. Ordinary writes replace that exemption. Copies to custom sanitizers receive the source's redacted snapshot, never internal raw capture values.

```csharp
using DRN.Framework.SharedKernel.Attributes;

public sealed class VerifyRequest
{
    [Sensitive]
    public string Code { get; init; } = string.Empty;

    [Personal]
    public string Email { get; init; } = string.Empty;

    public bool HasRecoveryCodes { get; init; }
}
```

`[Sensitive]` classifies sensitive data; `[Personal]` classifies personal information. Both use Microsoft compliance classification and emit `[redacted]`. Apply them to a class, struct, or captured property. Object logging omits public fields, including annotated fields. Scoped logs evaluate classified getters during capture and retain their redaction metadata. Snapshot reads never invoke them again. `[IgnoreLog]` getters remain excluded; direct sanitizer calls also skip classified getters. Passing `model.Password` directly to a logger evaluates that getter before the logger can inspect annotations. Other Microsoft `DataClassificationAttribute` classifications except `None` also require redaction. `AddDrnUtils` registers `DiagnosticRedactor` separately for `DataClassifications.Sensitive`, `DataClassifications.Personal`, and their combined `DataClassifications.Set`. Each resolves to the `[redacted]` marker through `IRedactorProvider`.

### Compile-time classification

SharedKernel distributes error-level diagnostics to directly and transitively dependent projects. All public readable instance non-indexer model properties are checked, including interfaces, generic declarations, positional records and generated models. Generated sources are analyzed, but DRN0015 through DRN0020 are not reported in generated code. The containing type need not be public. Logging usage, a model suffix and marker attributes are not required. Static properties, write-only properties, private getters and fields are excluded. `[IgnoreLog]` and ignored capture lists do not suppress classification diagnostics because classification also serves non-logging uses.

| Diagnostic | Meaning |
|---|---|
| DRN0015 | A matching property needs Personal, Sensitive, a derived classification, or direct Sensitive(false). |
| DRN0016 | Sensitive(false) has an invalid target or a derived attribute forwards false. |
| DRN0017 | A matching scalar name, untyped path, or external property name requires an explicit decision. Named model prefixes do not trigger built-in name rules. |
| DRN0018 | An unrelated Microsoft classification cannot be verified statically. Runtime non-None redaction remains supported. |
| DRN0019 | A matching anonymous property requires a named class or record with an annotated property. |
| DRN0020 | A captured dictionary with statically known matching keys requires AddDictionary or SanitizeDictionary. |

Company attributes may derive directly or indirectly from `SensitiveAttribute` or `PersonalAttribute`, including in referenced assemblies. Derived Sensitive attributes must remain positive; forwarding false throws during attribute construction and capture fails closed without reading getters. Existing parameterless constructors are retained. Do not use a custom attribute returning `None` as an exemption.

```csharp
public sealed class CompanyConfidentialAttribute : SensitiveAttribute;

public sealed class DiagnosticSummary
{
    [CompanyConfidential]
    public string Password { get; init; } = string.Empty;

    [Sensitive(false, Justification = "Keyboard layout name")]
    public string Keyboard { get; init; } = string.Empty;
}
```

Code fixes offer separate Personal and Sensitive actions, with Fix All for the selected classification. A separate Sensitive(false) action has no Fix All. DRN0016 offers `Use Sensitive(true)` for invalid opt-outs on types or fields and derived constructor calls. It replaces the argument without changing shared constants and requires individual selection. Generated and external declarations receive no direct-edit fix. Editor hosts must support the packaged .NET 10/Roslyn 5.9 analyzer and code-fix assemblies; command-line builds do not supply interactive fixes. No compatibility with older hosts is implied.

### Property false positives

Bare `[Sensitive(false)]` opts one property out of built-in name rules. `Justification` is optional. It represents `DataClassification.None`, while carrying an explicit DRN exemption. It is invalid on types and fields. Positive classification on the property, inherited property, declared property type or runtime value type still wins. `[IgnoreLog]` still prevents capture. An unchanged inherited property keeps its exemption; an override or hidden property needs its own. Interface annotations do not automatically annotate implementations. Interface-typed `AddProperties` capture checks both the declared property and its implementation for positive classification and ignore attributes, while retaining the declared property name.

False does not exempt descendants, dictionary keys or a parent prefix. Additional configured sensitive rules, capture limits and URL structural protection remain authoritative. `[IgnoreLog, Sensitive(false)]` satisfies the analyzer but remains ignored during capture.

### Typed models and untyped entries

Anonymous objects remain allowed when their property names do not match diagnostic name rules. Every anonymous creation is checked independently, including nested objects, collection entries, lambdas, inferred member names and generated code, even without logging. A matching name requires conversion to a named class or record with `[Sensitive]`, `[Personal]`, or `[Sensitive(false)]` for a reviewed false positive. An annotation on a source property does not transfer to an inferred anonymous property. Reason strings, capture policies and `[IgnoreLog]` do not bypass this requirement. This checks names, not the contents of values. Dictionary keys retain their existing capture rules.

For example, replace `new { StatusCode = 200, Password = password }` with an instance of:

```csharp
public sealed record ResponseDetails(
    [property: Sensitive(false, Justification = "HTTP response status.")] int StatusCode,
    [property: Sensitive] string Password);
```

Upgrading also diagnoses existing configuration projections and generated schema projections whose anonymous property names match. These require an explicit migration or a narrowly scoped, reviewed diagnostic suppression. There is no automatic record-conversion code fix.

Named classes and structs own classification through their property names and annotations. `AddProperties("ClientReport", model)`, `Add("ClientReport", model)` and `SanitizeValue("ClientReport", model)` do not apply built-in name rules to the model's logging prefix. Each typed property starts a fresh name check. Matching property names still need classification or `[Sensitive(false)]`. Configured `SensitiveFields` and `SensitiveFieldPaths` always check the complete capture path, including prefixes. Type classifications also remain authoritative in redacted views.

Anonymous objects, dictionaries and scalar entries retain structural name matching. Inside a typed property, untyped children match relative to that property's name, without the logging prefix. A typed collection property does not exempt its entries, and lazy collections are still not enumerated. Browser data is untyped and retains its existing full-path rules.

For one application-reviewed scalar, pass a nonblank reason directly. Supported values are strings, characters, booleans, signed/unsigned 8/16/32/64-bit integers, float, double, decimal, enums, Guid, DateTime, DateTimeOffset, DateOnly, TimeOnly, TimeSpan, and Uri. Nullable versions and null are accepted. Null remains null in sanitizer output; scoped logs retain their empty-string representation. The reason is for code review, not content-based validation. Blank or oversized reasons and unsupported values return an unavailable marker without traversal. Do not take exemption reasons from request input.

Anonymous objects, dictionaries, arrays, collections, arbitrary structs/classes, ScopeEvent and JsonElement are not supported by this overload, even if their contents are scalar. Use normal typed capture or exact policies for complex values. Non-finite floating-point values remain unavailable. DateOnly and TimeOnly retain their scalar representations. Uri values always pass through structural URL sanitization, regardless of the entry name, and remain subject to capture budgets.

```csharp
log.Add("Response_StatusCode", 503, exemptionReason: "HTTP response status only.");
sanitizer.SanitizeValue("Response_StatusCode", 503, exemptionReason: "HTTP response status only.");
```

These overloads retain positive classification, configured rules, URL sanitization and budgets. TimeSpan capture applies the exception to its generated `_Seconds` entry. Import `DRN.Framework.Utils.Logging.Diagnostics` for interface extension overloads. Custom loggers and sanitizers fall back to their existing behavior without exemptions.

### Dictionary classifications

Use `AddDictionary` for dictionaries with matching keys. `SanitizeDictionary` provides the same behavior without a scoped log. Both accept `IReadOnlyDictionary<string, TValue>` and `params DiagnosticKeyClassification[]`. Import `DRN.Framework.Utils.Logging.Diagnostics` for interface extension methods.

```csharp
log.AddDictionary("Response", values,
    new("Password", DiagnosticDataKind.Sensitive),
    new("Email", DiagnosticDataKind.Personal),
    new("StatusCode", DiagnosticDataKind.NonSensitive, "HTTP response status."));
```

Sensitive and Personal redact the entire value. Direct `SanitizeDictionary` skips traversal. Scoped `AddDictionary` captures values for Development-only unredacted reads, so getters may run. NonSensitive requires a nonblank, bounded application-authored reason and a supported scalar value. Invalid classifications fail the capture closed. A NonSensitive complex value produces an unavailable marker. Positive classifications win over conflicting NonSensitive entries, runtime type classifications remain protected, and configured rules always win. Key comparison is exact and case-sensitive. Missing classifications retain name-based protection.

Dictionary classification captures check each key independently of the logging prefix. Configured rules still inspect the full path. Classifications apply only at the current dictionary level. Nested dictionaries retain structural name matching and need separate classified capture when they contain matching keys. There is no subtree grant. URLs, capture budgets, detached snapshots and stricter-destination checks remain enforced. Foreign logger/sanitizer implementations receive an unavailable marker because they cannot enforce these classifications.

DRN0020 checks capture calls, including reason-bearing and policy-bearing calls. It recognizes constant keys in dictionary initializers, direct local assignments, and Add/indexer writes before capture, including nested dictionaries. Use AddDictionary or SanitizeDictionary for the matching dictionary. Harmless dictionaries and dictionary construction outside capture remain allowed. Dynamic keys, aliases, full reassignment flow and values returned from helper methods are not fully tracked. Runtime protection remains authoritative. AddDictionary acknowledges that boundary for its own keys but does not authorize nested matches. The diagnostic supplies guidance, not an automatic code fix.

### Exact capture exceptions

Construct `DiagnosticCapturePolicy` with `DiagnosticNameExemption` entries. Each entry describes a reviewed name-rule exemption, not a throwable exception.

Keep `AddWithPolicy`, `AddPropertiesWithPolicy` and `SanitizeWithPolicy` for multiple exceptions or specific nested nodes. Construct policies only in trusted application code. An empty policy grants nothing. Reason-bearing and policy-bearing calls acknowledge the runtime boundary without suppressing independent property-declaration diagnostics.

Exceptions identify the exact capture kind and original path segments with a required nonblank reason. `NamedEntry` identifies an Add/SanitizeValue root, `ObjectRoot` an AddProperties prefix, and Property or DictionaryKey identifies a child. ListItem appends an invariant numeric index segment, while name matching retains the collection's field path. Dotted keys are one segment, distinct from nested paths. Case/separator normalization does not broaden exception identity. Policies are immutable and bounded by capture limits. There are no wildcards or subtree grants.

For an anonymous object under `ClientError`, exempting the root permits traversal only. Its children still match the full untyped path and require separate decisions. Named models need no root exemption, but their individual properties retain classification and getter protection. Calling SanitizeText does not certify text as non-sensitive. A property carrying a dictionary cannot authorize its entries.

Copies between native scoped logs preserve exemptions only when the destination's configured rules permit them. Stricter name or URL rules may redact the entire copied diagnostic entry. Replacing a value removes its exemption. Re-adding a public snapshot applies ordinary protection and may redact an otherwise exempted value again.

### Boundaries and migration

DRN0019 exempts anonymous column definitions returned directly by EF `MigrationBuilder.CreateTable` callbacks and column selectors passed directly to `CreateTableBuilder` primary-key, unique-constraint and composite foreign-key APIs. Recognition uses resolved EF symbols and column-operation metadata types. Migration directories are not excluded. Ordinary data objects, nested default-value objects and logging calls inside migrations remain checked, with diagnostics suppressed in generated code.

DRN0017 and DRN0020 skip instance calls directly chained from NSubstitute `Received`, `DidNotReceive`, `ReceivedWithAnyArgs` or `DidNotReceiveWithAnyArgs`, including count overloads. These calls verify interactions rather than capture data. Recognition uses the resolved NSubstitute method, not its name alone. Ordinary calls on substitutes and independent model or anonymous-property declarations remain checked. Verification aliases and wrappers are not inferred.

The analyzer checks declarations independently of logging and supplements them with constant-path analysis at known capture methods. Named models use their own property names; anonymous and untyped paths retain prefix matching. Unknown prefixes, reason-bearing and policy-bearing APIs, arbitrary wrappers, deserialized keys and runtime subtypes still require runtime protection. It does not execute getters or arbitrary attribute constructors. Referenced company attributes are recognized by their DRN base symbols; a consumer compilation cannot recompile every model in external assemblies. Classify generated properties through generator inputs or supported partial declarations when needed for runtime capture.

This upgrade can break builds containing matching unclassified properties. Review each finding, select the correct classification, or explicitly declare a false positive. Do not mass-apply Sensitive or disable all test analysis. Standard per-diagnostic suppression is available for deliberate runtime regression fixtures and staged migration, but does not disable runtime protection. Excluding transitive analyzer assets also prevents compiler enforcement.

Positive classifications and `[IgnoreLog]` on interface properties also protect concrete and nested captures, including inherited implementations. A positive interface classification overrides an implementation's `[Sensitive(false)]`. Interface exemptions do not waive concrete property name rules. An unrelated explicit interface implementation does not classify a same-named public property.

Copied URLs and query strings retain source protection and also apply destination rules.

The shared [rule catalog](data-classification-rules.json) matches substrings after removing separators and normalizing ASCII case. Built-in rules check typed property names and untyped structural paths. Configured rules always check full paths. Patterns such as `secret`, `token`, `client`, `cred`, `conn`, `code`, and `key` protect matching names unless an applicable explicit exception permits that node. Secret matching uses `secret`, so `Seconds`, `Section`, and `ResponseCaptureLimited` do not match merely because they contain `sec`. Unmatched text fields retain their contents within capture limits. URL segment rules use the same matching to redact the following segment and cannot be waived by name exceptions.

The catalog also protects `cvv`, `cvc`, and `pincode` as secret fields, and `username`, `firstname`, `lastname`, `dateofbirth`, `birthdate`, and `iban` as personal fields. Separator and case variants match too. Broad patterns such as `name`, `address`, `pin`, `pan`, and `otp` are not included: they would match ordinary names such as `FileName`, `ServiceAddress`, `ShippingMethod`, `Panel`, and `RootPath`. Classify secret-bearing OTP properties explicitly with `[Sensitive]`.

For untyped dictionaries or forms, extend `DiagnosticLogging` with `SensitiveFields`, `SensitiveFieldPaths`, or `SensitivePathSegments`. Custom rules also use normalized substring matching; rules that normalize to empty are ignored. Additional rules cannot remove built-in protection. Bootstrap `ScopedLog(IAppSettings)` uses the same configuration as DI. Direct duration-field additions and copied snapshots remain eligible for capture unless another rule matches their path; dedicated metric methods check the operation name before generating their metric keys.

```csharp
public sealed class Operation(IScopedLog log)
{
    public void Capture(VerifyRequest input, Exception failure)
    {
        log.AddProperties("Verification", input);
        log.AddException(failure, "Verification failed");
    }
}
```

Content-based redaction is not performed. Free text, exception messages, stack text, text bodies, and unmatched string values are retained within capture limits, including assignments, credentials, email addresses, private-key blocks, and embedded URLs. Classify sensitive fields or omit sensitive text before capture. `IDiagnosticSanitizer.TryRegisterSensitiveValue` and the `MaxSensitiveValues`, `MaxSensitiveValueBytes`, and `MaxSensitiveBytes` options are no longer available; migrate callers and custom sanitizer implementations to field rules or classification attributes.

## Project classification rules

Applications can extend the built-in catalog with one `data-classification-rules.json`. The current consumers enforce diagnostic classification and redaction, not storage or ordinary API serialization. Files only add protection. Omitted groups and empty arrays leave built-in rules intact.

```json
{
  "secretFields": ["challengeResponse"],
  "personalFields": ["membershipNumber"],
  "sensitiveFieldPaths": ["payload.privateValue"],
  "secretPathSegments": ["handoff"]
}
```

Register the project's own file as an embedded resource:

```xml
<ItemGroup>
  <EmbeddedResource Include="data-classification-rules.json" />
</ItemGroup>
```

Analyzers inherit catalogs from referenced projects that consume SharedKernel, including transitive references. Rebuild dependencies with SharedKernel's build targets to make their catalogs available. Catalogs from unrelated assemblies are ignored.

To load those same embedded/inherited catalogs at runtime, name the application assembly:

```json
{
  "DiagnosticLogging": {
    "RuleAssemblyName": "My.Application"
  }
}
```

Runtime includes catalogs from referenced SharedKernel consumers. Missing transitive dependencies are skipped. Invalid inherited catalogs fail loading. No loose catalog file needs to be deployed for this mode. Separately loaded plugins outside the application's references are not implicitly included.

Alternatively, register a loose file as an AdditionalFiles item. Referencing projects inherit these rules too. If runtime uses RuleFile, copy the same file to build and publish output:

```xml
<ItemGroup>
  <AdditionalFiles Include="data-classification-rules.json" />
  <None Update="data-classification-rules.json"
        CopyToOutputDirectory="PreserveNewest"
        CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

For a shared file outside the project directory, use `AdditionalFiles Include="../shared/data-classification-rules.json"` and a `None Include` item with `Link="data-classification-rules.json"` and the same copy metadata, or an EmbeddedResource item pointing to the shared file. Keep one own catalog per project. Registering the same physical file as both AdditionalFiles and EmbeddedResource is deduplicated by the build targets. Distinct files are rejected. Referenced projects publish their own catalogs through generated metadata; do not repeat their file items in consuming projects. Avoid duplicate runtime output files when centralizing registration.

Configure runtime loading from that deployed file:

```json
{
  "DiagnosticLogging": {
    "RuleFile": "data-classification-rules.json"
  }
}
```

Direct callers can set RuleFile or RuleAssemblyName on DiagnosticLogOptions. Both can be configured and merge additively. Relative file paths resolve from `AppContext.BaseDirectory`, not the working directory. Leaving both null preserves built-in-only behavior. Missing, unreadable, or invalid configured sources fail sanitizer creation. Runtime inheritance skips transitive assembly references that cannot be found, so undeployed optional dependencies do not prevent discovery. Catalogs in unavailable assemblies are not inherited. Other assembly load failures and invalid catalogs still fail creation. Each options instance loads once; snapshots and native copies retain loaded protection without rereading the file. Use fresh configured options to load changed loose-file contents. Embedded catalogs require rebuilding their owning assemblies. This is not live reload.

The analyzer reads Roslyn AdditionalFiles and eligible assembly metadata once per compilation. Build items also accept the legacy filename `diagnostic-rules.json`. Including distinct own files, including both names, reports DRN0021. Runtime reads explicitly configured sources and does not search directories. Invalid files or inherited metadata report DRN0021 while built-in analysis remains active. Rule files are JSON objects with the four case-sensitive group names above. Groups must be string arrays. Unknown or duplicate groups, nulls, names without ASCII letters/digits, names over 512 characters, more than 256 rules per file, and files over 65,536 characters are rejected. Inheritance is bounded to 1,024 assemblies, 64 catalogs, and 4,096 distinct rules. Use UTF-8 for shared files. Catalogs contain rule names, never secret values.

Project secret/personal fields extend catalog name matching and permit the existing reviewed name exemptions. Project `sensitiveFieldPaths` protect full capture paths and cannot be waived by name exemptions. URL segment rules protect the following segment and remain non-exemptible. Positive classification still wins. Existing runtime `SensitiveFields`, `SensitiveFieldPaths`, and `SensitivePathSegments` settings remain additive and are not read by the analyzer. The compiler checks statically known names and capture paths; dynamic paths still depend on runtime enforcement.

The browser imports the built-in catalog at bundle time. Server RuleFile settings do not automatically reach the browser. Configure browser rules separately when client-side capture requires the same project-specific protection.

## Supported capture and limits

Form bodies match configured field paths against the supplied source path and decoded field name. For example, `Checkout.Value` protects the `Value` form field when the source path is `/Checkout`. URL query and fragment keys remain independent of the URL path.

Browser array capture retains at most the first 256 positions, followed by a limit marker when truncated. Holes become null, getters are not invoked, and sparse indices cannot expand the captured array beyond this bound.

The sanitizer supports text, URLs, UTF-8 text/form bodies, primitives, dictionaries, lists, scope events, and DTO projections. Object projections capture readable public instance properties only. Public fields are omitted, including on nested objects. Name-based redaction still applies to properties, dictionary keys, and explicitly named log entries. It produces detached values. It skips indexers, catches getter failures, detects cycles, and replaces unsupported lazy collections. It never falls back to an arbitrary object's `ToString`. Application getters cannot be preempted, so use small purpose-built diagnostic DTOs.


Explicit URL capture and fields ending in `Url` or `Uri` remove user information and apply named query and path rules. Fragments containing `=` use field-name rules for their pairs; bare fragments retain their URL representation without content inspection. Fields ending in `QueryString` and form bodies apply field-name rules to their pairs. Supported percent encodings are decoded before matching; bare fragments are not decoded for matching. Unmatched values, including nested URL strings inside query values, receive no content inspection. Invalid encodings encountered during matching produce unavailable markers. Plain text is never parsed as JSON, assignments, or URLs.

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

HTTP error response capture reads at most the field limit plus one byte, restores seekable stream positions, and does not dispose the response. Unsupported encodings/content types and failed reads produce markers. No new request-body capture is enabled.

## Emission and reconstruction

Call `logger.LogScoped(log)` or `logger.LogScoped(log, LogLevel.Warning)` for explicit severity. Disabled levels skip snapshotting and formatting. Diagnostics from custom `IScopedLog` implementations are sanitized at emission. Supply the application's sanitizer when custom field or URL rules are needed. Sink failures are contained without retries and can lose an entire emission.

A fitting diagnostic payload remains one structured `@Logs` record. Larger diagnostic snapshots use one parent and bounded child records. Retained events and any `OmittedEventCount` appear only on the parent, outside the reconstructed diagnostic payload. Event contents can make that record exceed configured byte limits. Every record contains:

- `DiagnosticGroupId`, a new ID for this emission.
- `LogRecordId` and `ParentLogRecordId`, formatted as `group/index`.
- `PartIndex` and `PartCount`, counting the parent at index zero.
- `RelatedLogRecordIds`, listing the parent and all expected children.
- Applicable `CorrelationId`, real `TraceId`, `TraceIdentifier`, and `ClientError.ClientErrorId`.

The event category `EventId` is independent of these record references. Each child has `PayloadEncoding: "json-text"`, a zero-based `PayloadFragmentIndex`, and `PayloadFragmentCount` excluding the parent. Concatenate `PayloadFragment` strings in index order to reconstruct the bounded sanitized JSON. Structural and attribute redaction happens before fragmentation. Total-limit loss remains explicit in the reconstructed snapshot. Very large context fields may become limit markers.

Delivery is best effort and not atomic. Compare received record IDs with `RelatedLogRecordIds` to detect missing delivery. `OmittedEventCount` counts events excluded by the scope limit, not records lost during delivery. `ScopedLog.ToString()` returns one JSON snapshot containing diagnostics and retained events. Providers may impose their own record limits.

## Protection boundary

Covered diagnostic outputs include scoped additions, exceptions, copies and exports, Hosting request diagnostics, Flurl exception diagnostics, startup reporting, developer model/view copies, and Sample browser reports with independent server enforcement. Configuration debug-view protections retain their own contract.

Scope event contents bypass sanitization, including custom sanitizers. Keep them safe to log. Custom `IScopedLog` implementations expose events through `Event` and `ScopeEvent` values under `AdditionalEvents`. Event-shaped dictionaries remain ordinary diagnostics.

Original exceptions and `HttpContext` remain available to filters and `Microsoft.AspNetCore.Diagnostics.UnhandledException` subscribers for compatibility. Subscribers own their export policy. Arbitrary direct logger calls, historical logs, and unclassified secrets are not automatically protected.
