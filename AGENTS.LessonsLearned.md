# Lessons Learned

## 1. DTT Data Attributes

### Parameter Resolution

All DTT data attributes (`DataInline`, `DataMember`, `DataSelf` and `Unit` variants) resolve parameters identically:

| Step | What Happens |
|------|-------------|
| 1. Context | `DrnTestContext` or `DrnTestContextUnit` auto-provided when the method signature requests it |
| 2. Inline values | Attribute arguments mapped to subsequent method parameters in order |
| 3. AutoFixture | Remaining parameters without inline values auto-generated (primitives, `Guid`, POCOs, etc.) |
| 4. NSubstitute | Interface/abstract-class parameters auto-mocked; mocks auto-replace matching registrations in `ServiceCollection` |

### Attribute Variants

| Integration Test | Unit Test |
|-----------------|----------|
| `[DataInline]` | `[DataInlineUnit]` |
| `[DataMember]` | `[DataMemberUnit]` |
| `[DataSelf]` | `[DataSelfUnit]` |

Integration variants provide `DrnTestContext` when requested; Unit variants provide `DrnTestContextUnit` when requested (lightweight — no `ContainerContext`, `ApplicationContext`, or `FlurlHttpTest`). Use `[Fact]` when a test has no inline data, generated parameters, or context dependency.

### Attribute Examples

```csharp
[Fact]
public void Trim_Should_Remove_Outer_Whitespace()
{
    "  Duran  ".Trim().Should().Be("Duran");
}

[Theory]
[DataInline(AppEnvironment.Development, true)]
public void Integration_DataInline_Should_Request_Context_When_Needed(
    DrnTestContext context, AppEnvironment environment, bool expected)
{
    context.AddToConfiguration(new { Environment = environment.ToString() });
    (environment == AppEnvironment.Development).Should().Be(expected);
}

[Theory]
[DataInlineUnit(2, 3, 5)]
public void Unit_DataInlineUnit_Should_Omit_Context_When_Unused(int a, int b, int expected)
{
    (a + b).Should().Be(expected);
}

[Theory]
[DataInlineUnit("SafeSection", "Visible", "safe-value")]
public void Unit_DataInlineUnit_Should_Request_Context_When_Needed(
    DrnTestContextUnit context, string section, string key, string value)
{
    context.AddToConfiguration(section, key, value);
    var debugView = context.GetConfigurationDebugView();

    debugView.SettingsByProvider.Values.SelectMany(settings => settings)
        .Should().Contain($"{section}:{key}={value}");
}
```

### Test Consolidation

If tests share the same setup and their consolidation creates no semantic or performance issue, they should be unified. Apply when consolidation requires only minimal essential change.

#### Parameterized

**When**: Logic has discrete input→output permutations and test body is identical across cases — consolidate into one `[Theory]` with multiple `[DataInline(...)]` / `[DataInlineUnit(...)]` rows.

**Anti-pattern**: Separate `[Theory]` methods per input permutation with identical bodies (e.g., 5 methods, ~65 lines).

**Preferred** (1 method, ~17 lines):

```csharp
[Theory]
[DataInlineUnit(AppEnvironment.Development, true, false, true)]   // Dev + AutoMigrateDev=on  → migrate
[DataInlineUnit(AppEnvironment.Development, false, true, false)]  // Dev + AutoMigrateDev=off → no migrate
[DataInlineUnit(AppEnvironment.Staging, true, false, false)]      // Staging ignores Dev flag
[DataInlineUnit(AppEnvironment.Staging, false, true, true)]       // Staging + AutoMigrateStaging=on → migrate
[DataInlineUnit(AppEnvironment.Production, true, true, false)]    // Production → never migrate
public void Migrate_Flag_Should_Reflect_Environment_And_AutoMigrate_Settings(DrnTestContextUnit context,
    AppEnvironment environment, bool autoMigrateDevelopment, bool autoMigrateStaging, bool migrationEnabled)
{
    ConfigureEnvironment(context, environment, autoMigrateDevelopment, autoMigrateStaging);
    var status = context.GetRequiredService<DevelopmentStatus>();
    var model = CreateChangeModel();
    status.AddChangeModel(model);
    model.Flags.Migrate.Should().Be(migrationEnabled);
}
```

#### Flow

**When**: Tests share identical setup (container init, migrations, service registration) and assertions can continue in the same flow — unify into a single test.

**Reference**: `QAContextTagTests.cs` — single flow validating entity IDs, JSON queries, date filters, and materialization interceptor.

#### Rules

1. **Last parameter = expected result** — each `[DataInlineUnit]` row is a self-contained specification
2. **Name covers the dimension** — e.g., `..._Should_Reflect_Environment_And_AutoMigrate_Settings`, not a name tied to one specific case
3. **Comment inline data** — trailing comment on each attribute row when values aren't self-explanatory
4. **Extract shared setup** — private helper keeps the test body focused on act + assert
5. **Omit inline values for auto-generated params** — let AutoFixture/NSubstitute handle params you don't need to control
6. **Omit context when unused** — `[DataInlineUnit]` and `[DataInline]` do not require `DrnTestContextUnit` / `DrnTestContext` parameters unless the test uses the context
7. **Use `[Fact]` for no-parameter tests** — do not add a DTT context just to satisfy an attribute convention
8. **Don't consolidate when** — test bodies differ structurally, require different setup/teardown, or separate failure messages aid debugging more than parameterization

## 2. Per-Request Allocation 

### Context

Early `SelectRateLimitPartition` code in `DrnProgramBase` called `rules.OrderBy(r => r.Order)` inside the partition-selector callback, which runs on every request. Since singleton rate limit rules are stable at runtime, their order does not need to be recomputed per request.

### Problem

`IEnumerable<IRateLimitRule>.OrderBy(...)` allocates a new `OrderedEnumerable` + iterator on every request. On the pre-auth hot path (before auth, before any short-circuiting), this is avoidable allocation under high throughput.

### Fix Applied

Rate limiting rules are split by lifetime:

- Singleton rules: register as `ISingletonRateLimitRule`, resolve from root `IServiceProvider` once, sort once, and compose into the native chained limiter.
- Scoped rules: register as `IScopedRateLimitRule`, detect existence/order at startup in a temporary scope, and resolve from the request provider only when present.

This avoids per-request sorting for the common singleton-only case while preserving scoped rule support.

### Decision Checkpoint

Prefer singleton rate limit rules unless evaluation truly needs scoped collaborators. Scoped rules are more flexible, but they keep per-request resolution in the hot path.

## 3. Rate Limiting — Scoped Rules Belong After Authentication

### Context

Scoped rate limit rules can depend on request-scoped services such as `IScopedUser`, tenant accessors, or per-request lookup services. Pre-auth limiting runs before authentication and before `ScopedUserMiddleware` populates the authenticated user.

### Problem

Resolving scoped rate limit rules in pre-auth creates a lifetime-safe but semantically fragile path: constructors or cached scoped collaborators may observe an unauthenticated user even when the rule is intended for post-auth evaluation.

### Fix Applied

- Pre-auth limiter evaluates singleton rules only.
- Post-auth limiter composes singleton and scoped rules.
- Rule execution preserves global `Order` across singleton and scoped rules. `ShortCircuitOnMatch` prioritizes same-order allow/deny rules; when a short-circuit rule returns `null`, later rules still evaluate.
- DRN emits a dedicated `DRN.Framework.Hosting.RateLimiting` meter so pre-auth limiting remains observable even though it runs outside ASP.NET Core's built-in rate limiting middleware.

### Decision Checkpoint

Use singleton rules for pre-auth IP/header/service-key partitions. Use scoped rules only for post-auth user/tenant/account policies that need scoped collaborators.

For claim-based scoped partitions, prefer app-specific `RateLimitFor` wrappers (e.g., `Sample.Hosted.Helpers.RateLimitFor`) with `IScopedUser` so claims are read from the cached scoped user model instead of repeatedly parsing `HttpContext.User`.

## 4. Rate Limiting — Preserve ASP.NET Core Named Policies

### Context

DRN post-auth rate limiting extends ASP.NET Core's `RateLimiterOptions` with a global rule chain. Applications may also configure named policies and rejection callbacks through `builder.Services.AddRateLimiter(options => ...)` and apply policies with `[EnableRateLimiting("policy-name")]`.

### Problem

Creating a new `RateLimiterOptions` instance in the hosting pipeline discards DI-configured named policies and rejection callbacks, making endpoint metadata look supported while policy lookup fails at runtime and custom rejection behavior silently disappears.

### Fix Applied

`CreatePostAuthRateLimiterOptions` starts from `IOptions<RateLimiterOptions>.Value`, then applies DRN's global limiter and wraps rejection handling. This preserves policy registrations and existing rejection callbacks while keeping DRN telemetry/logging and the default 429 response.

Rule-level `PolicyName` now filters DRN rules by the same `[EnableRateLimiting("policy-name")]` endpoint metadata. This keeps named endpoint behavior compatible with ASP.NET Core while avoiding a separate DRN policy engine.

DRN tracks the rule that actually rejected separately from the last rule that matched. Native named policies can reject after the DRN global limiter succeeds, so rejection callbacks and logs must use the rejected-rule marker, not the last selected match.

### Decision Checkpoint

When extending framework options, mutate/enrich the DI-configured options object unless there is a documented reason to isolate from user configuration.
When composing global DRN rules with native named policies, distinguish rule selection from rejection attribution before invoking rule-specific `OnRejectedAsync` behavior.

## 5. Configuration — Nested Option Objects Need Explicit Validation

### Context

`DrnAppFeatures` is validated through DRN's data-annotation helper, which calls `Validator.TryValidateObject` on the root configuration object.

### Problem

Plain data-annotation validation does not automatically walk nested option objects. Moving rate-limit settings under `DrnAppFeatures:DrnRateLimit` would keep the configuration shape cleaner, but nested `[Range]` attributes would not fail fast unless `DrnAppFeatures` explicitly validates the child object.

### Fix Applied

`DrnAppFeatures` exposes the C# property as `RateLimit`, binds it from the external `DrnRateLimit` configuration key, and validates it as part of root validation, preserving fail-fast behavior for nested operational settings while keeping code usage concise.

### Decision Checkpoint

When grouping configuration into nested objects, add or verify recursive validation before relying on child data annotations for startup safety.

## 6. Rate Limiting — Convenience Base Classes Should Own DI Registration

### Context

Rate limit rules can be created by deriving from `SingletonRateLimitRule` / `ScopedRateLimitRule` or by implementing `ISingletonRateLimitRule` / `IScopedRateLimitRule` directly.

### Problem

Documentation forced every rule to repeat DI attributes even when the selected base class already encoded the intended lifetime. That made the convenience base classes less convenient and made examples noisier.

### Fix Applied

`SingletonRateLimitRule` and `ScopedRateLimitRule` now carry the lifetime attributes. The DI scanner honors inherited lifetime attributes while letting direct attributes override inherited ones.

### Decision Checkpoint

When a base class exists primarily to encode a convention, put the convention on the base class. Keep explicit attributes for direct interface implementations and unusual cases.

## 7. ScopeContext, ScopedUser, and Claim Accessors

### Context

`ScopeContext` is the ambient per-request facade initialized by hosting middleware. Sample Hosted exposes it through `Get.Claim`, `Get.Role`, and similar helpers for Razor/page ergonomics. `IScopedUser` is the cached scoped user model populated from the current principal.

### Problem

Using ambient `ScopeContext` everywhere is convenient, but it blurs boundaries:

- UI and page helpers benefit from `Get.*` accessors.
- Services, scoped rules, and reusable framework helpers should prefer injected `IScopedUser` or explicit value parameters.

### Fix Applied

Keep app-specific claim vocabulary in hosted applications, such as `Sample.Hosted.Helpers.RateLimitFor`, and compose it from claim-access primitives like `Get.Claim.<DomainClaim>.*`. For example: account partitions must read Account, tenant partitions must read Tenant.

### Decision Checkpoint

Use `ScopeContext` through `Get.*` for Razor, tag helpers, and view/page convenience. Use `IScopedUser` for services and rate-limit rules. For claim partitions, read and validate the same claim that is formatted into the key; return `null` when the rule does not apply instead of producing empty or mixed-claim partition keys.

When unit tests exercise `Get.*` helpers, mock the ambient context through `DrnTestContextUnit` and `ScopedUser.FromClaimsPrincipal(...)`, then call `ScopeContext.InitializeForTest(...)` inside each test. `InitializeForTest(...)` resets the async-local scope first, so tests can safely call it more than once without carrying stale `ScopeData`, users, or services forward.

## 8. WebApplicationFactory Entry Points Must Stay Out of MTP Test Assemblies

### Context

`WebApplicationFactory<TEntryPoint>` resolves and executes the entry point for the assembly containing `TEntryPoint`. In `DRN.Test.Integration`, Microsoft Testing Platform also generates an xUnit entry point for the test executable.

### Problem

Defining a custom hosted `Program` inside the integration test assembly can make `WebApplicationFactory` execute the test runner entry point instead of the intended hosted app. With MTP/xUnit this can surface as `System.IndexOutOfRangeException` in `Microsoft.Testing.Platform.CommandLine.CommandLineParser.Parse(...)` because the host factory path invokes the generated test main with web host arguments.

### Fix Applied

Put custom integration-test host programs in a non-test support assembly such as `DRN.Test.Utils` using `Microsoft.NET.Sdk.Web`, `IsTestProject=false`, and a real `Main` that delegates to `DrnProgramBase.RunAsync`. The integration test project references that support assembly and calls `CreateClientAsync<TProgram>()` with the support assembly's program type.

### Decision Checkpoint

Keep test assertions in `DRN.Test.Integration`; keep reusable disposable app entry points in `DRN.Test.Utils`. Add new scenarios under focused namespaces in the utility assembly instead of embedding hosted program types in the MTP test executable.

## 9. Rate Limiting — Model Deny as a First-Class Result

### Context

Rate limit rules already had `AllowRequest(...)`, quota partitions, `stopRemainingRules`, and `ShortCircuitOnMatch`. Deny behavior was implied by exhausting a limiter, which made allow/deny/short-circuit semantics easy to confuse.

### Problem

Using zero-capacity or already-exhausted limiter options to represent deny mixes policy decisions with quota mechanics and can depend on algorithm-specific option validation.

### Fix Applied

`RateLimitRuleResult.DenyRequest(...)` creates an explicit failed lease via a small rejecting limiter. `RateLimitRuleAction` carries the selected action (`Limit`, `Allow`, or `Deny`) instead of exposing separate allow/deny booleans. This keeps deny semantics independent from token bucket, fixed window, sliding window, and concurrency limiter settings while still flowing through DRN's normal 429, telemetry, and rule `OnRejectedAsync` path.

### Decision Checkpoint

Use `AllowRequest(...)` for trusted bypass, `DenyRequest(...)` for immediate policy rejection, quota helpers for measurable throttling, and `ShortCircuitOnMatch` only to control same-order precedence and remaining-rule evaluation.

## 10. Rate Limiting — Separate Policy Caching from Quota Enforcement

### Context

`DrnRateLimitOptions` provides global defaults, while enterprise B2B SaaS often needs tenant plans, feature flags, custom endpoint limits, and hard quotas across many app replicas.

### Problem

It is tempting to treat `HybridCache` / `IDistributedCache` as enough to make rate limiting distributed. They are useful for sharing policy data, but DRN's built-in token/fixed/sliding/concurrency limiters still keep quota counters in each process. Rule evaluation is also synchronous because ASP.NET Core partition selection is synchronous.

### Fix Applied

Document `DrnRateLimitOptions` as global defaults, push tenant-specific decisions into rules, expose `RateLimitRuleAction` as telemetry, use deterministic keyed hashes for rejected partition logs by default, and document that hard multi-replica quotas need edge enforcement or a custom Redis-backed `RateLimiter` returned by `RateLimitRuleResult.CustomPartition(...)`.

### Decision Checkpoint

Use `HybridCache` for tenant-plan and feature-flag snapshots, preferably loaded before rule evaluation or refreshed into memory. Use Redis Lua/atomic operations, API gateway, CDN/WAF, or service mesh policy for hard distributed quota enforcement.

For rejected partition logging, prefer keyed hashes over plain redaction or reversible encryption in ordinary logs. Use `PlainText` only for controlled development or a dedicated encrypted audit sink with retention and access controls.

## 11. Vite Manifest Discovery in Staging

### Context

`Sample.Hosted` can run with `Environment=Staging` locally while ASP.NET Core static web assets still point to source `wwwroot` content roots through `*.staticwebassets.runtime.json`.

### Problem

`ViteManifest` scanned only `IWebHostEnvironment.WebRootPath`. In Staging-from-build-output scenarios, that path can be the build output `wwwroot` while the Vite manifest files are served from static-web-asset content roots. Razor tag helpers then emit `<!-- Vite entry ... not found -->`, leaving the page without CSS/JS.

### Fix Applied

Manifest discovery now includes the configured manifest root, adjacent `wwwroot`, app-base `wwwroot`, and content roots from `*.staticwebassets.runtime.json`. It only loads Vite's `.vite/manifest.json` files, because `build.manifest: true` writes there by default and ordinary `wwwroot/manifest.json` files can be PWA/import-map/app metadata. The manifest cache is cleared if the root changes. All scripts (including preload and inline configuration scripts) are deferred or run as ES modules by default, executing in order of appearance in the document.

### Decision Checkpoint

When changing environment defaults from Development to Staging, verify static asset discovery as well as server startup. A page that renders can still be missing Vite assets if manifest lookup silently returns an empty cache. Make sure all scripts are designed to execute correctly in deferred/module contexts.

## 12. CodeRabbit Custom Guidelines Sync

### Context

CodeRabbit AI reviews pull requests but defaults to standard language practices. Custom architecture constraints (like DTT tests, attribute-based DI, and source-known IDs) are easily missed.

### Problem

Manually duplicating the repository rules (e.g. from `AGENTS.md` and `DiSCOS.md`) inside the `.coderabbit.yaml` file is redundant, prone to drift, and hitting path instruction length limits.

### Fix Applied

Explicitly enabled `knowledge_base.code_guidelines` in `.coderabbit.yaml` and set `filePatterns` to feed `AGENTS.md` and `.agent/rules/DiSCOS.md` as standard rules. CodeRabbit dynamically scans these files to align reviews with the project’s specific architectural rules.

### Decision Checkpoint

Instead of bloating path instructions with generic text, use `reviews.path_instructions` only for path-specific overrides (like framework vs sample layers). Feed global project guidelines (like `AGENTS.md`) directly via `knowledge_base.code_guidelines.filePatterns`.

## 13. File Hashing Should Stream Large Inputs

### Context

`HashExtensions.HashOfFile` and `HashOfFileWithKey` originally loaded the entire file with `File.ReadAllBytes` before hashing.

### Problem

Whole-file reads create avoidable large allocations and duplicate memory pressure for file and payload hashing. This is especially costly for asset manifests, uploads, and other hot paths where the hash algorithm can already process incremental chunks.

### Fix Applied

Add `Stream` overloads for `Hash`, `HashToBinary`, `Hash64Bit`, and `HashWithKey`, then route file hashing through `File.OpenRead`. BLAKE3 stream hashing uses a pooled 16 KiB chunk buffer aligned with BLAKE3's SIMD guidance and clears it before returning it to the pool. The string-returning `Hash(Stream, ...)` overload encodes directly instead of routing through `BinaryData`.

### Decision Checkpoint

For files and large payloads, prefer stream overloads over `BinaryData` or byte arrays. Keep byte-array and `BinaryData` overloads for already-materialized data.

## 14. NuGet buildTransitive Targets Need Exact Package Paths

### Context

`DRN.Framework.Hosting` ships a custom `buildTransitive` target so consuming web SDK projects include Vite manifests during publish.

### Problem

Using a folder-only `PackagePath` such as `buildTransitive/` can combine with item metadata like `RecursiveDir=buildTransitive/`, placing the file under a nested package path. NuGet then sees a `.targets` file somewhere under `buildTransitive/` but not the convention file `buildTransitive/{PackageId}.targets`, raising `NU5129` when warnings are treated as errors.

### Fix Applied

Set the package metadata to the full target file path, `buildTransitive/$(PackageId).targets`, and guard it with a unit test that reads the project file.

### Decision Checkpoint

For package `build`, `buildMultiTargeting`, and `buildTransitive` `.props` / `.targets` files, use the exact convention package path instead of relying on folder-only `PackagePath` behavior.

## 15. Docker Scout Reads .NET Shared-Framework Metadata from deps.json

### Context

RID-specific, framework-dependent Docker publishes can emit `.deps.json` entries for shared frameworks such as `Microsoft.NETCore.App` and `Microsoft.AspNetCore.App`. Vulnerability scanners may treat those metadata entries as installed packages even when the final image supplies a patched shared runtime.

### Problem

For framework-dependent apps, shared-framework metadata in `.deps.json` must stay deterministic and match the runtime image patch. Mixing an exact `RuntimeFrameworkVersion` with legacy patch-floating flags can obscure which patch the artifact was built against and make scanner evidence harder to reason about.

### Fix Applied

Keep the final ASP.NET runtime image patch and the restore/build/publish metadata aligned. Docker builds pin the runtime version once and pass `RuntimeFrameworkVersion`, `SelfContained=false`, and `UseAppHost=false` consistently through restore, build, and publish. Do not add `TargetLatestRuntimePatch` when the exact runtime patch is already supplied.

### Decision Checkpoint

When upgrading .NET Docker runtime patches, update the Docker runtime image tag and the publish metadata together. If a scanner still reports a non-applicable CVE after metadata is aligned, attach a Docker Scout exception or VEX statement rather than deleting required `.deps.json` files.

## 16. Interface Substitutes Do Not Exercise Concrete Convenience Methods

### Context

`IAppSettings.GetDebugView(...)` is implemented by `AppSettings`, but unit tests may use `Substitute.For<IAppSettings>()` to control only the configuration/environment surface.

### Problem

Calling a convenience method on an interface substitute returns NSubstitute's configured/default value; it does not execute the concrete `AppSettings` method. This can silently turn a redaction or mapping test into a null/default-value test unless the method is explicitly configured.

### Fix Applied

Use the real concrete implementation when testing the public convenience method. When testing a collaborator that only needs the interface data, construct that collaborator directly with the substitute.

### Decision Checkpoint

If the behavior under test lives in a concrete class, instantiate that class. Use interface substitutes for dependencies, not for methods whose implementation is the subject of the assertion.

## 17. TODO: Re-evaluate Identity Bearer MFA Semantics

### Context

Bearer/MFA hardening was explored and then removed because it became confusing among broader security fixes. The discussion mixed ambient MFA state (`ScopeContext.User.Amr`), bearer token issuance, refresh token principal handling, and configured MFA exemption schemes.

### Current Understanding

`MfaFor.MfaCompleted` is the simple ambient request check and should remain the default mental model. `MfaExemptionMiddleware` currently represents configured scheme exemptions, not a fully named "MFA-satisfying scheme" abstraction.

### Future TODO

Re-evaluate Identity bearer authentication in an isolated change. Decide whether Identity bearer should be treated as a true MFA-exempt scheme, should satisfy MFA only through ambient `ScopeContext` when `amr=mfa` is present, or needs a separately named request-level concept. If revisited, keep the design small, document the chosen contract, and add focused tests for password-only bearer, MFA bearer, refresh, and mixed authentication schemes.

## 18. Object-to-JSON Configuration Uses CamelCase Keys

### Context

`AddObjectToJsonConfiguration(...)`, `DrnTestContext.AddToConfiguration(object)`, and `AppSettings.Development(object...)` serialize option objects with the framework's global `System.Text.Json` defaults before loading them through `JsonStreamConfigurationProvider`.

### Problem

Those JSON defaults use camelCase property names. Tests that add a `ConnectionStringsCollection` object should expect keys like `connectionStrings:testDb`, while tests that explicitly add an in-memory key such as `ConnectionStrings:testDb` should expect the exact caller-provided casing.

### Decision Checkpoint

When asserting `ConfigurationDebugView` output, match the configuration source. Object-based configuration follows JSON naming policy; explicit key/value configuration preserves the key text supplied by the test.

## 19. Configuration Sections Can Have Both Values and Children

### Context

`ConfigurationDebugView` walks `IConfigurationRoot.GetChildren()` and resolves each path back to the provider that supplies the visible value.

### Problem

.NET configuration sections are not strictly containers or leaves. One provider can define a scalar parent such as `ConnectionStrings`, while another provider defines `connectionStrings:testDb`. Treating the scalar parent as terminal hides child keys. Reusing the merged traversal path for rendered entries can also leak the parent provider's casing into a child value from another provider, which can surface only in CI where environment variables add extra parent values.

### Fix Applied

Record the parent value when present, then continue recursing into `child.GetChildren()` so sibling providers' child keys remain visible and redacted. For each entry, render the path using the provider that supplied the value, not the merged section path inherited during traversal.

### Decision Checkpoint

When traversing configuration, never assume `IConfigurationSection.Value != null` means the section has no children. Always inspect children independently if the view must be complete. When asserting `ConfigurationDebugView` paths, simulate mixed-provider casing (`ConnectionStrings` parent plus `connectionStrings:testDb` child) so local tests cover CI-like environment-provider interactions.

## 20. MTP Test Projects Run Through `dotnet run`

### Context

`DRN.Test.Unit` and `DRN.Test.Integration` are executable Microsoft Testing Platform/xUnit v3 test projects.

### Problem

`dotnet test` invokes the legacy VSTest target and is rejected on .NET 10 MTP projects. Standard `--filter` syntax is also not accepted by the generated xUnit v3 test executable.

### Fix Applied

Run test projects with `dotnet run --project <test-csproj>`. For focused xUnit v3 runs, pass runner filters after `--`, such as `--filter-class Fully.Qualified.TestClass` or `--filter-method Fully.Qualified.TestMethod`.

### Decision Checkpoint

Use `dotnet run --project DRN.Test.Unit/DRN.Test.Unit.csproj` for unit validation and only run integration after unit tests pass. Do not use `.slnx` in test commands.

## 21. GitHub Action SHA Pins Must Match Tag Commits

### Context

CI workflows pin third-party GitHub Actions by full commit SHA and keep the intended version as an inline comment.

### Problem

Manually copied SHAs can look plausible while not existing in the upstream action repository. GitHub resolves `uses: owner/action@<sha>` as a ref; if the SHA is mistyped or mixed with a nearby tag value, the workflow fails before the action starts.

### Fix Applied

Verify every pinned action with `git ls-remote --tags <repo> refs/tags/<version> refs/tags/<version>^{}` and use the dereferenced tag commit when the tag is annotated. If a pinned SHA is suspicious, confirm it with `git fetch --depth=1 <repo> <sha>`; upstream should not return `not our ref`.

### Decision Checkpoint

When updating pinned GitHub Actions, do not trust visual SHA prefixes or generated comments. Validate tag-to-SHA mapping from the official action repository before committing.

## 22. Docker Scout Needs an Explicit Image Target

### Context

The release workflows build and push multi-platform Docker images through `docker/build-push-action`, then run `docker/scout-action` from the shared `docker-publish` composite action.

### Problem

When `docker/scout-action` runs without an explicit `image:` input, Scout may analyze the most recent image known to the runner instead of the image just built by DRN. In agentic or tool-rich runners, that can surface unrelated helper images such as `ghcr.io/github/gh-aw-firewall/agent:latest` and produce irrelevant CVEs or base-image recommendations.

### Decision Checkpoint

Always pass the exact pushed DRN image reference to Docker Scout, preferably by repository plus the `docker/build-push-action` digest output. Treat third-party helper-image recommendations as non-actionable for DRN unless the workflow intentionally consumes that image.

## 23. Release Tags Must Match CI Triggers and Docker Metadata

### Context

DRN release workflows, git conventions, and agent skills all describe the release contract that maintainers follow.

### Problem

If workflow tag filters expect bare `v...` while the release contract says `release/v...`, the intended namespaced release tags do nothing. Preview Docker metadata can also accidentally emit stable `major.minor` tags if prerelease tags are matched by the stable tag rule.

### Fix Applied

Release workflows listen to documented `release/v*.*.*` and fixed-width `release/v*.*.*-previewNNN` tags, strip the `release/v` prefix for package versions, and Docker metadata emits `major.minor` tags only for stable releases. Preview releases keep the prerelease semver tag.

### Decision Checkpoint

Whenever tag patterns change, update all three surfaces together: workflow triggers, version extraction, and Docker metadata rules. Verify preview tags cannot overwrite stable Docker tags, and keep preview numbers fixed width for natural ordering.

## 24. Docker Scout Severity Filters Should Be Explicit

### Context

`docker/scout-action` supports `critical`, `high`, `medium`, `low`, and `unspecified` severity filters. When `exit-code: true` is enabled, the selected severities define which CVEs can block a release.

### Problem

Removing `only-severities` makes Scout consider every severity, including `unspecified`. That can turn unknown-severity findings into release blockers with weaker prioritization signal than known low/medium/high/critical CVEs.

### Fix Applied

Keep `only-severities` explicit and include `critical,high,medium,low` so known low severity CVEs block releases while `unspecified` findings remain reported without becoming the release gate.

### Decision Checkpoint

For release gates, prefer explicit severity filters over relying on an empty/default filter. Add `unspecified` only if the team intentionally wants unknown-severity findings to block publishing.
