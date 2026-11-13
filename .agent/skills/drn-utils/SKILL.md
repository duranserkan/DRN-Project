---
name: drn-utils
description: "DRN utilities for DI, configuration, scoped diagnostics, HTTP, Source-Known IDs, cancellation, and MFA."
last-updated: 2026-11-13
difficulty: intermediate
---

# DRN.Framework.Utils

Use the installed `DRN.Framework.Utils` README and XML documentation for API signatures and examples. [Locate the package](../overview-drn-framework/SKILL.md#documentation-and-source-references)

## Registration and configuration

Scan the application's service assembly with `services.AddServicesWithAttributes(typeof(OrderService).Assembly)`. Annotate the implementation with `[Scoped<IOrderService>]`. Import `DRN.Framework.Utils.DependencyInjection` for the extension and `DRN.Framework.Utils.DependencyInjection.Attributes` for the attribute.

- Register each module through its extension method and scan its assembly with `AddServicesWithAttributes()`. Use the scoped, singleton, transient, keyed, or hosted-service attributes. Their default `TryAdd` preserves existing registrations. Opt out when registering multiple implementations. Keep registration idempotent per service collection and validation scoped to the provider being validated.
- `AddDrnUtils()` supplies HybridCache and the system TimeProvider. Register a distributed cache first if required. Use `[Config]` for a section and `[ConfigRoot]` for the root. Validate nested options explicitly.
- Follow [configuration source precedence](../overview-drn-framework/SKILL.md#maintenance-reference-configuration-sources). Use double underscores for nested environment keys. Configuration debug views redact secret-looking values by default. Keep raw views restricted.
- Configure `DrnAppDataSettings__TempPath` and `DrnAppDataSettings__DataPath` before loading DRN settings. Use `GetPath(...)` on the selected `IAppData` root for child paths. Preserve traversal and symlink checks.

### Nexus Keys

Configure `NexusAppSettings:AppId` (0 through 127) and `AppInstanceId` (0 through 63) explicitly in every environment. Zero is valid. Concurrent generators in the same partition need distinct instance IDs. Configure exactly one default Nexus key. All keys must decode to 32 bytes using the declared UTF-8, Hex, Base64, or Base64Url encoding. Write with the default key and retain previous keys for rotation reads.

## ID Generation

`Validate<TEntity>` checks the entity's declared type and application partition. `Validate(id, entityType)` uses the configured partition. Select another explicitly with `Validate<TApp>` or an `EntityTypeId`. Never trust a caller-constructed ID record at an external boundary.

`ConfiguredDefault` selects Secure or Plain from `UseSecureSourceKnownIds`. Secure requires decryption and verification. Plain skips decryption. Auto tries plain then encrypted forms. Keep format overrides under application control. Authenticate an external GUID in the required format before conversion. Conversions revalidate using Auto. Undefined formats fail even for null IDs. Apply authorization and rate limits separately. See [SharedKernel validation](../drn-sharedkernel/SKILL.md#validation-approaches).

Configure epoch and minimum before first use. Generation, decoding and filtering must share an epoch across a dataset. Use `EntityCreatedFilter` for date boundaries. Inclusive boundaries admit the whole 250 ms tick, exclusive boundaries exclude it, and reversed endpoints normalize. [Domain filtering](../drn-domain-design/SKILL.md#api-integration)

## Scoped logging and classification

Use `IScopedLog` for operation diagnostics. Use code-owned metric names. A scoped log owns a correlation ID and captures a W3C trace ID only when one exists. HTTP request IDs and native activity fields have separate meanings. Copies retain the destination's correlation and primary event.

Scope events bypass diagnostic sanitization and budgets. Supply safe event data. Keep one primary and up to eight additional events on the first emitted record, with the omission count when needed. Ordinary diagnostic values remain bounded on every record.

Classify dynamic backend values at capture. Compiler name/path checks and browser rules do not redact server values at runtime. Explicit URL capture removes credentials and validates encoding. Explicit Sensitive/Personal values redact as `[redacted]`. Free text keeps its contents within capture budgets. Check the installed package’s logging documentation for its capture limits and overflow behavior.

- Use `AddDictionary` or `SanitizeDictionary` for exact key classifications. NonSensitive requires a reason and a supported scalar. Preserve positive classifications and configured analyzer paths. Native copies retain detached values and classification metadata within destination budgets. Custom sanitizers receive redacted snapshots.
- Preserve inherited and mapped interface classifications, ignored members, single getter evaluation in scoped capture, and getter suppression in direct sanitization. Register redactors for Sensitive, Personal and their combination. See [catalog and analyzer rules](../drn-sharedkernel/SKILL.md#attributes).
- Use typed diagnostic models or selected scalars for payload details. Unsupported nonempty body types, including JSON, receive the unsupported marker. Keep explicit URL capture separate from ordinary text and diagnostic JSON output.

## HTTP and ambient context

Inject `IExternalRequest` for external endpoints or `IInternalRequest` for service traffic. Inject a handler for test routing. Buffered converters dispose the response even on read failure. Dispose streaming wrappers after consumption.

Try converters preserve readable HTTP statuses and payloads, return processing failures separately and propagate cancellation. `ThrowIfFailure()` rethrows a captured failure with its original stack. Check status explicitly when a readable error response should fail the operation. Keep failure messages and exceptions out of public responses and redact them before logging.

Use `ScopeContext` only after Hosting initializes a request or a test calls `InitializeForTest`. Use injected services elsewhere. `ScopeContext.Problem` supplies missing public response fields. Keep diagnostics private. Its exception factories throw immediately. [Hosting error mapping](../drn-hosting/SKILL.md#error-responses)

## Scoped Cancellation

`ICancellationUtils.Root` affects all present and future children in the DI scope. Merging an external token or canceling a keyed child affects only that group. To cancel one operation, create and dispose a local linked token source after all consumers finish.

Use typed `CancellationScopeKey.For<T>()` for a component, optionally with a constant name. Ownerless keys share an ordinal, case-sensitive namespace. Names are non-null, at most 128 characters and may be empty. Never derive them from requests or instance IDs. The default key is invalid. Reusing a key returns the same scope and stable token. Canceled scopes stay canceled. The service owns child scopes. Disposal remains safe inside cancellation callbacks. See [repository cancellation](../drn-entityframework/SKILL.md#repository-cancellation).

## Authentication and other utilities

- Use the configured canonical claim types and aliases. Require unambiguous subjects and issuers. Assurance checks require completed MFA and trusted issuer evidence on the same authenticated identity. Authentication time alone cannot prove recent MFA. Phishing resistance requires an explicit assurance mapping. The [Hosting skill](../drn-hosting/SKILL.md) owns policy integration and issuance.
- TOTP defaults to six digits, 30-second steps and one step of drift. Verification is stateless. Enforce atomic per-account replay protection and attempt limits before issuing authentication evidence.
- Use stream hashing for large inputs, bounded stream reads, strict RFC 4648 Base32 and the structural JPEG validator for their respective formats. Consult the Utils README for byte encodings, bit packing, merge patch, pagination and validators.
- `RecurringAction` runs synchronous callbacks. Use `RecurringActionAsync` for asynchronous work and a token-aware callback for timeouts. Stop requests cancellation. Async restart and disposal wait for prior work. A callback that ignores cancellation can block them.
- Prefer `[UnsafeAccessor]` for known, accessible declaring types. Static methods require the exact declaring type. Use reflection for runtime-discovered or inaccessible types.
