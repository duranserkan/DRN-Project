---
name: drn-hosting
description: "DRN hosting: startup, authentication/MFA, API errors, rate limits, Razor endpoints and TagHelpers. Keywords: hosting, drnprogrambase, security, csp, nonce, authentication, authorization, razor-pages, mfa"
last-updated: 2026-11-04
difficulty: advanced
tokens: ~2.7K
---

# DRN.Framework.Hosting

Apply `AGENTS.md` and DiSCOS. Document public behavior; keep internals in source and local paths in the profile.

Use [version-matched references](../overview-drn-framework/SKILL.md#documentation-and-source-references). The [Hosting README][hosting] owns signatures, examples, defaults, and migration. Development guidance may be unreleased. `Example*` names are placeholders.

## Contents

- [DrnProgramBase Pattern](#drnprogrambase-pattern)
- [Security Features](#security-features)
- [Page & Endpoint Management](#page--endpoint-management)
- [Error Responses](#error-responses)
- [Rate Limiting Rules](#rate-limiting-rules)
- [TagHelpers](#taghelpers)
- [Related Skills](#related-skills)

## DrnProgramBase Pattern

Derive from `DrnProgramBase<TProgram>`, implement `IDrnProgram` and `AddServicesAsync`. Use public/protected hooks, not internal configurators. README **Lifecycle & Execution Flow** and **DrnProgramBase Deep Dive** own timing and base-call rules.

See README **QuickStart** and global usings. `DrnProgramActions` offers `ApplicationBuilderCreatedAsync` and `ApplicationValidatedAsync` hooks.

Configure Source-Known epoch/minimum before startup or first ID/epoch use. Temporary hosts and `SkipValidation` do not bypass time validation. Temporary applications skip endpoint validation/accessor population, enter service validation, run `ApplicationValidatedAsync`, and return without starting. Use explicit `DrnDevelopmentSettings` test flags; never weaken runtime checks with environment branches. See [testing](../drn-testing/SKILL.md).

Razor uses IDE/`dotnet watch` Hot Reload, not runtime compilation. Execution needs authorization. Framework overview and Utils own configuration, DI, app data, environment conventions, and `[HostedService]` registration.

## Security Features

### MFA by Default

MFA applies by default, including role and named-policy endpoints. `[AllowAnonymous]` permits public access; `AuthPolicy.MfaExempt` permits authenticated access without MFA.

- Register an effective default or policy-selected authentication scheme. Exemption allowlists do not select schemes.
- Ordinary Identity apps need no claim-mapping override. Use `ConfigureAuthenticationClaims()` for custom providers.
- Identity apps register `AddSignInManager<DrnSignInManager<TUser>>()` and `AddDrnIdentityMfaPolicies()`.
- `ConfigureMFARedirection()` supplies local setup/challenge pages; `null` disables redirection.
- Retain base Identity renewal callbacks. Refresh is not fresh MFA.
- External providers need validated handlers; shared MFA authorization does not require Identity.

README **MFA** owns assurance, renewal, revocation, audit events, and replacing default/fallback policies while retaining registered MFA policies.

### GDPR & Consent

`DrnDefaults` withholds non-essential response cookies until consent. Apps own script activation, category preferences, and `IsEssential` classification. README **Cookies and consent** owns cookie settings and exposed consent state.

### Swagger And Developer Tool Authorization

Swagger is Development-only by default. To enable elsewhere, call base in `ConfigureSwaggerOptions`, set `AddSwagger = true`, and assign a named `AuthorizationPolicy`. It protects UI, assets, and JSON/YAML documents, including custom document routes. MFA still applies. Null preserves existing authorization behavior; blank policy names are invalid.

Enabled Swagger requires a nonempty slash-trimmed `SwaggerUIOptions.RoutePrefix`, e.g. `api-docs`. Root mounts are rejected. Reserve that subtree for Swagger CSP and put ordinary pages elsewhere. Disabled Swagger retains application policies. Customize `ConfigureSecurityHeaderPolicyBuilder` while retaining base behavior. See README **Route-specific security headers**.

## Page & Endpoint Management

Static accessors use `PageCollectionBase<ExamplePageFor>.PageCollection` and `(ExampleEndpointFor)EndpointCollectionBase<ExampleProgram>.EndpointCollection!`. Pages self-initialize; API metadata requires endpoint initialization. Never retain request-specific URLs or authorization results statically.

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

DRN exceptions retain status (default 500). Flurl preserves upstream 400-499/503/504; other statuses or no response become 502. `BadHttpRequestException` retains `StatusCode` (default 400); other exceptions become 500. Names/inner exceptions do not choose status. Preserve cancellation, started-response, and abort behavior; never replace a response that can no longer be changed.

Problem Type precedence: response, scope, status default, `about:blank`. Type overrides never change status. Leave Type unset for routine failures. Explicit types identify stable public conditions beyond status, never CLR names, routes, or request data.

API statuses 400-599 use Problem Details in every environment with `JsonConventions.DefaultOptions`, not MVC/HTTP JSON overrides. Matched endpoints use API metadata; unmatched routes/routing 405s use `DrnApiErrorOptions.PathPrefixes` (default empty). Preserve auth decisions, headers, cookies, validation, and extensions. `requestId` is `HttpContext.TraceIdentifier`; default instance is PathBase + Path without query. Keep exceptions and serialization fallbacks private/redacted.

`ScopeContext.Problem` fills missing fields/keys without overriding standard fields or validation. `[DisableDrnProblemDetails]` opts controllers/actions out of formatting and Swagger error metadata, retaining native responses/authentication. README **API Failure Responses** owns customization callbacks and Swagger response metadata.

`HandleHtmxRequests` defaults false. Opt in for HTMX Problem Details, including Razor handlers. Preserve redirects, auth, opt-outs, and custom non-problem bodies. Razor `Page()` selects HTML; `ObjectResult` containing `ValidationProblemDetails(ModelState)` selects JSON at 400/422. Use safe messages, exact posted field names, and an empty key for form-level errors. Preserve existing API keys. See README **API Failure Responses**, **HTMX Partial Requests**, and [browser handling](../drn-buildwww-libraries/SKILL.md#problem-details-consumer).

`DrnErrorPageOptions` configures browser 400/404/5xx pages. Keep them anonymous, non-cacheable, and independent of application data. Preserve status, request reference, deployment prefixes, redaction, existing bodies, Development diagnostics, and unmatched-route fallback authorization. Supported API exceptions bypass `WriteProductionErrorResponseAsync` unless opted out.

For access-denied references, use `AccessDeniedPageModel` and `PreserveAccessDeniedRequestId()`. Follow README **Custom Error Pages** for cookie-event ordering, callback preservation, single-use TempData, and `EventsType` setup.

Follow [Utils](../drn-utils/SKILL.md) and the profile's diagnostic guide. Apps own dynamic classification. `SanitizeText` does not certify content; runtime names do not redact. Keep private diagnostics out of public errors, including raw callbacks.

See [Developer Diagnostics](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Hosting/README.md#developer-diagnostics) and [Startup Exception Reports](https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Hosting/README.md#startup-exception-reports) for privacy and failure-logging rules.

## Rate Limiting Rules

README **Rate Limiting** owns settings/examples; **Telemetry** owns metrics/log fields.

- Derive from `SingletonRateLimitRule`/`ScopedRateLimitRule` for automatic DI. Return null when inapplicable; otherwise return a `RateLimitRuleResult` quota, custom partition, allow, or deny result.
- Lower `Order` runs first; rules compose. `Action` and `StopRemainingRules` are distinct. `ShortCircuitOnMatch` prioritizes same-order allow/deny; lower Order bypasses earlier quotas. Null permits later evaluation.
- `[DisableRateLimiting]` bypasses pre-auth quota; `[EnableRateLimiting]` does not. `PolicyName` targets matching named policies; null is global, blank invalid. Post-auth retains DI-configured policies/rejection callbacks.
- Defaults: post-auth 250/minute; pre-auth IP bucket 2,500/minute. `DrnAppFeatures:DrnRateLimit` is a startup snapshot; zero phase values inherit shared settings. App rules own tenant/account/endpoint quotas.
- Use stable user IDs plus scheme, not display names. Scoped rules use `IScopedUser`/`RateLimitFor`. Preload policy data for synchronous evaluation. Partition factories receive immutable values, never captured HttpContext/scoped services.
- Rejected IP/partition fields use keyed hashes, not whole-log anonymization. PlainText is for controlled development/encrypted audit sinks. State is process-local; hard multi-replica quotas require edge/custom distributed enforcement, not HybridCache alone.
- README **Telemetry** owns the `DRN.Framework.Hosting.RateLimiting` meter, traffic/phase boundaries, rejection counts, and allow/deny distinctions.

## TagHelpers

Activate in the applicable `_ViewImports.cshtml`:

```razor
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, DRN.Framework.Hosting
```

See README **Razor TagHelpers** for Vite/SRI, CSP nonces, HTMX CSRF, active links, opt-outs, and script defaults. Buildwww skills own asset layout.

`authorized-only`/`anonymous-only` are presence-only markers using `ScopeContext.Authenticated`; even `"false"` enables filtering. Omit the marker or use Razor conditions to disable it. They do not check completed MFA. `policy-only="PolicyName"` checks the current user with optional `policy-resource`; invalid policy names fail. Visibility never inspects linked endpoints or replaces endpoint authorization.

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
