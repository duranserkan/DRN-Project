# Lessons Learned

## 1. DTT Data Attributes

### Parameter Resolution

All DTT data attributes (`DataInline`, `DataMember`, `DataSelf` and `Unit` variants) resolve parameters identically:

| Step | What Happens |
|------|-------------|
| 1. Context | `DrnTestContext` or `DrnTestContextUnit` auto-provided as first parameter when the method signature requests it |
| 2. Inline values | Attribute arguments mapped to subsequent method parameters in order |
| 3. AutoFixture | Remaining parameters without inline values auto-generated (primitives, `Guid`, POCOs, etc.) |
| 4. NSubstitute | Interface/abstract-class parameters auto-mocked; mocks auto-replace matching registrations in `ServiceCollection` |

### Attribute Variants

| Integration Test | Unit Test |
|-----------------|----------|
| `[DataInline]` | `[DataInlineUnit]` |
| `[DataMember]` | `[DataMemberUnit]` |
| `[DataSelf]` | `[DataSelfUnit]` |

Integration variants provide `DrnTestContext`; Unit variants provide `DrnTestContextUnit` (lightweight — no `ContainerContext`, `ApplicationContext`, or `FlurlHttpTest`).

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
6. **Don't consolidate when** — test bodies differ structurally, require different setup/teardown, or separate failure messages aid debugging more than parameterization

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

### Decision Checkpoint

When extending framework options, mutate/enrich the DI-configured options object unless there is a documented reason to isolate from user configuration.

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

When unit tests exercise `Get.*` helpers, mock the ambient context through `DrnTestContextUnit` and `ScopedUser.FromClaimsPrincipal(...)`, then call `ScopeContext.InitializeForTest(...)` inside each test.
