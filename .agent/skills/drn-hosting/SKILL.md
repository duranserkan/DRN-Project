---
name: drn-hosting
description: "Configure DRN hosts, MFA, API errors, rate limits, Razor endpoints, CSP and TagHelpers."
last-updated: 2026-11-13
difficulty: advanced
---

# DRN.Framework.Hosting

Use [version-matched references](../overview-drn-framework/SKILL.md#documentation-and-source-references). The [Hosting README][hosting] owns signatures, examples, defaults, and migration. Development guidance may be unreleased. `Example*` names are placeholders.

## DrnProgramBase Pattern

Derive from `DrnProgramBase<TProgram>`, implement `IDrnProgram` and `AddServicesAsync`. Use public/protected hooks, not internal configurators. README **Lifecycle & Execution Flow** and **DrnProgramBase Deep Dive** own timing and base-call rules.

See README **QuickStart** and global usings. `DrnProgramActions` offers `ApplicationBuilderCreatedAsync` and `ApplicationValidatedAsync` hooks.

Configure Source-Known epoch/minimum before startup or first ID/epoch use. Temporary hosts and `SkipValidation` do not bypass time validation. Temporary applications skip endpoint validation/accessor population, enter service validation, run `ApplicationValidatedAsync`, and return without starting. Use explicit `DrnDevelopmentSettings` test flags. Never weaken runtime checks with environment branches. See [testing](../drn-testing/SKILL.md).

Razor uses IDE/`dotnet watch` Hot Reload, not runtime compilation. Execution needs authorization. Framework overview and Utils own configuration, DI, app data, environment conventions, and `[HostedService]` registration.

## Security Features

### MFA by Default

MFA applies by default, including role and named-policy endpoints. `[AllowAnonymous]` permits public access. `AuthPolicy.MfaExempt` permits authenticated access without MFA.

- Register an effective default or policy-selected authentication scheme. Exemption allowlists do not select schemes.
- Ordinary Identity apps need no claim-mapping override. Use `ConfigureAuthenticationClaims()` for custom providers.
- Identity apps register `AddSignInManager<DrnSignInManager<TUser>>()` and `AddDrnIdentityMfaPolicies()`.
- `ConfigureMFARedirection()` supplies local setup/challenge pages. `null` disables redirection.
- Retain base Identity renewal callbacks. Refresh is not fresh MFA.
- External providers need validated handlers. Shared MFA authorization does not require Identity.

README **MFA** owns assurance, renewal, revocation, audit events, and replacing default/fallback policies while retaining registered MFA policies.

### GDPR & Consent

`DrnDefaults` withholds non-essential response cookies until consent. Apps own script activation, category preferences, and `IsEssential` classification. README **Cookies and consent** owns cookie settings and exposed consent state.

### Swagger And Developer Tool Authorization

Swagger is Development-only by default. To enable elsewhere, call base in `ConfigureSwaggerOptions`, set `AddSwagger = true`, and assign a named `AuthorizationPolicy`. It protects UI, assets, and JSON/YAML documents, including custom document routes. MFA still applies. Null uses fallback authorization. Blank policy names are invalid.

Swagger UI prefixes use nonempty ASCII letter, digit, hyphen or underscore segments. Root mounts are rejected. Reserve the UI subtree for Swagger CSP. Disabled UI retains application policies. Retain base behavior in `ConfigureSecurityHeaderPolicyBuilder`. The [Hosting README][hosting] owns `AddSwaggerUI`, metadata, document URLs, title defaults, upstream caching and `DrnPageOptions.FaviconUrl`.

## Page & Endpoint Management

Static accessors use `PageCollectionBase<ExamplePageFor>.PageCollection` and `(ExampleEndpointFor)EndpointCollectionBase<ExampleProgram>.EndpointCollection!`. Pages self-initialize. API metadata requires endpoint initialization. Never retain request-specific URLs or authorization results statically.

```csharp
public class ExamplePageFor : PageCollectionBase<ExamplePageFor>
{
    public UserPageFor User { get; } = new();
}

public class UserPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["User"];
    public string Login { get; init; } = string.Empty;
}

public class TagFor() : ControllerForBase<ExampleTagController>(ExampleApiFor.ControllerRouteTemplate)
{
    public ApiEndpoint GetAsync { get; private set; } = null!;
}
```

README **Endpoint Management** covers endpoint collection grouping/initialization.

Prefer MVC page/action routing or `Url.Endpoint(endpoint, values)` for initialized APIs. Use `~/` for application-relative HTML/HTMX URLs and `Url.Content` in C#. Reserve `Request.ApplicationUrl` for configured destinations such as MFA pages. Preserve validated return URLs. See README **Application URLs**.

Use `ForPage` for page identities and `ForUrl` for URLs. Active href matching uses local request paths without queries/fragments. See [Razor accessors](../frontend-razor-accessors/SKILL.md) for presentation.

`IPageUtils.RenderPageAsync` supports `_ViewStart`, layouts, and links for unmatched requests. It asynchronously disposes its own DI scope on success/failure. `parentContext` preserves `PathBase`/nonce without transferring scope ownership.

## Error Responses

DRN exceptions retain status (default 500). Flurl preserves upstream 400-499/503/504. Other statuses or no response become 502. `BadHttpRequestException` retains `StatusCode` (default 400). Other exceptions become 500. Names/inner exceptions do not choose status. Preserve cancellation and abort behavior. Never replace a started response.

Problem Type precedence: response, scope, status default, `about:blank`. Type overrides never change status. Leave Type unset for routine failures. Explicit types identify stable public conditions beyond status, never CLR names, routes, or request data.

API statuses 400-599 use Problem Details in every environment with `JsonConventions.DefaultOptions`, not MVC/HTTP JSON overrides. Matched endpoints use API metadata. Unmatched routes/routing 405s use `DrnApiErrorOptions.PathPrefixes` (default empty). Preserve auth decisions, headers, cookies, validation, and extensions. `requestId` is `HttpContext.TraceIdentifier`. Default instance is PathBase + Path without query. Keep exceptions and serialization fallbacks private/redacted.

`ScopeContext.Problem` fills missing fields/keys without overriding standard fields or validation. `[DisableDrnProblemDetails]` opts controllers/actions out of formatting and Swagger error metadata, retaining native responses/authentication. README **API Failure Responses** owns customization callbacks and Swagger response metadata.

`HandleHtmxRequests` defaults false. Opt in for HTMX Problem Details, including Razor handlers. Preserve redirects, auth, opt-outs, and custom non-problem bodies. Razor `Page()` selects HTML. `ObjectResult` containing `ValidationProblemDetails(ModelState)` selects JSON at 400/422. Use safe messages, exact posted field names, and an empty key for form-level errors. Preserve existing API keys. See README **HTMX Partial Requests** and [browser handling](../drn-buildwww-libraries/SKILL.md#problem-details-consumer).

`DrnPageOptions` configures browser 400/404/5xx pages. Keep them anonymous, non-cacheable, and independent of application data. Preserve status, request reference, deployment prefixes, redaction, existing bodies, Development diagnostics, and unmatched-route fallback authorization. Supported API exceptions bypass `WriteProductionErrorResponseAsync` unless opted out.

For access-denied references, use `AccessDeniedPageModel` and `PreserveAccessDeniedRequestId()`. Follow README **Custom Error Pages** for cookie-event ordering, callback preservation, single-use TempData, and `EventsType` setup.

Follow [Utils](../drn-utils/SKILL.md) and the profile's diagnostic guide. Apps own dynamic classification. `SanitizeText` does not certify content. Runtime names do not redact. Keep private diagnostics out of public errors, including raw callbacks.

See [Developer Diagnostics](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Hosting/README.md#developer-diagnostics) and [Startup Exception Reports](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Hosting/README.md#startup-exception-reports) for privacy and failure-logging rules.

## Rate Limiting Rules

README **Rate Limiting** owns settings/examples. **Telemetry** owns metrics/log fields.

- Derive from `SingletonRateLimitRule`/`ScopedRateLimitRule` for automatic DI. Return null when inapplicable. Otherwise return a `RateLimitRuleResult` quota, custom partition, allow, or deny result.
- Lower `Order` runs first and quotas compose. `Action` and `StopRemainingRules` are distinct. `ShortCircuitOnMatch` runs first at the same order and stops later rules on a match. Place bypass rules before quotas. Earlier quotas and native policies still apply. Null permits later evaluation.
- `[DisableRateLimiting]` bypasses pre-auth quota. `[EnableRateLimiting]` does not. `PolicyName` targets matching named policies. Null is global, blank invalid. Post-auth retains DI-configured policies/rejection callbacks.
- Defaults: post-auth 250/minute and pre-auth IP bucket 2,500/minute. `DrnAppFeatures:DrnRateLimit` is a startup snapshot. Zero phase values inherit shared settings. App rules own tenant/account/endpoint quotas.
- Use stable user IDs plus scheme, not display names. Scoped rules use `IScopedUser`/`RateLimitFor`. Preload policy data for synchronous evaluation. Partition factories receive immutable values, never captured HttpContext/scoped services.
- Rejected IP/partition fields use keyed hashes, not whole-log anonymization. PlainText is for controlled development/encrypted audit sinks. State is process-local. Hard multi-replica quotas require edge/custom distributed enforcement, not HybridCache alone.

## TagHelpers

Activate in the applicable `_ViewImports.cshtml`:

```razor
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, DRN.Framework.Hosting
```

See README **Razor TagHelpers** for Vite/SRI, CSP nonces, HTMX CSRF, active links, opt-outs, and script defaults. Buildwww skills own asset layout.

`authorized-only`/`anonymous-only` are presence-only markers using `ScopeContext.Authenticated`. Even `"false"` enables filtering. Omit the marker or use Razor conditions to disable it. They do not check completed MFA. `policy-only="PolicyName"` checks the current user with optional `policy-resource`. Invalid policy names fail. Visibility never inspects linked endpoints or replaces endpoint authorization.

```razor
<a asp-page="/User/Login" anonymous-only>Sign In</a>
<button type="button" policy-only="EditDocument" policy-resource="@Model.Document">Edit</button>
```

Publish Vite manifests with their emitted assets. See README **Static Asset Compression** for build-time integrity hashes and Brotli-only entries.

## Related Skills

- [Framework overview](../overview-drn-framework/SKILL.md): shared conventions.
- [EntityFramework](../drn-entityframework/SKILL.md): dependency launch.
- [Razor layout](../frontend-razor-pages-shared/SKILL.md): UI lifecycle.

[hosting]: https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Hosting/README.md
