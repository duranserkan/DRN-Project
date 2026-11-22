---
name: drn-hosting
description: "Configure DRN hosts, MFA, API errors, rate limits, Razor endpoints, CSP and TagHelpers."
last-updated: 2026-11-22
difficulty: advanced
---

# DRN.Framework.Hosting

Use [version-matched references](../overview-drn-framework/SKILL.md#documentation-and-source-references). The [Hosting README][hosting] owns full signatures, examples, and defaults. `Example*` names are placeholders.

## DrnProgramBase Pattern

Derive from `DrnProgramBase<TProgram>` implementing `IDrnProgram` and `AddServicesAsync`. Use public/protected hooks, not internal configurators. `DrnProgramActions` exposes `ApplicationBuilderCreatedAsync` and `ApplicationValidatedAsync`.

Configure Source-Known epoch/minimum before startup or first ID/epoch use. Temporary hosts and `SkipValidation` never bypass time validation. Temporary apps skip endpoint validation/accessor population, run service validation and `ApplicationValidatedAsync`, and return without starting. Use explicit `DrnDevelopmentSettings` test flags and never weaken runtime checks with environment branches ([testing](../drn-testing/SKILL.md)).

Razor uses IDE/`dotnet watch` Hot Reload, not runtime compilation. Execution requires authorization. Framework overview and Utils own config, DI, app data, environment rules, and `[HostedService]`.

## Security Features

### MFA by Default

MFA applies by default (including role and named-policy endpoints). `[AllowAnonymous]` allows public access. `AuthPolicy.MfaExempt` allows authenticated access without MFA.

- Register an effective default or policy-selected auth scheme (exemption allowlists do not select schemes).
- Identity apps register `AddSignInManager<DrnSignInManager<TUser>>()` and `AddDrnIdentityMfaPolicies()`. Override `ConfigureAuthenticationClaims()` only for custom providers.
- `ConfigureMFARedirection()` sets local setup/challenge pages (`null` disables redirection).
- Keep base Identity renewal callbacks (refresh is not fresh MFA). External providers need validated handlers. Shared MFA does not require Identity.
- README **MFA** owns assurance, renewal, revocation, audits, and custom policy overrides.

### GDPR & Consent

`DrnDefaults` withholds non-essential cookies until consent. Apps own script activation, category preferences, and `IsEssential` (README **Cookies and consent**).

### Swagger And Developer Tool Authorization

Swagger defaults to Development only. Elsewhere, call base in `ConfigureSwaggerOptions`, set `AddSwagger = true`, and assign a named `AuthorizationPolicy` protecting UI, assets, and OpenAPI docs (including custom routes). MFA still applies. Null uses fallback authorization. Blank policy names are invalid.

Swagger UI prefixes require nonempty ASCII letter, digit, hyphen, or underscore segments and reject root mounts. Reserve the UI subtree for Swagger CSP. Disabled UI keeps app policies. Retain base behavior in `ConfigureSecurityHeaderPolicyBuilder`. README owns `AddSwaggerUI`, metadata, doc URLs, title defaults, caching, and `DrnPageOptions.FaviconUrl`.

## Page & Endpoint Management

README **Source-Known Controllers** owns service-backed `SourceKnown*Controller` (`protected` `Repository`, `ISourceKnownService<TEntity, TResponse>`, virtual `CreateResponseAsync`/`CreateResponsesAsync`/`CreateEntityAsync`). Use installed `DRN.Framework.SharedKernel` `CONVENTIONS.md` (**Terminology**) via [version-matched lookup](../overview-drn-framework/SKILL.md#documentation-and-source-references) for canonical Domain, Subdomain, project stem, and role stem terms. `DRNAUTHORIZATION001` requires named policies. SPA APIs use bearer-only schemes and cookie APIs need antiforgery. Keep resource checks in app code. Disable actions with `[NonAction]` and omit accessors (`IgnoreApi` only hides docs).

Return typed `EndpointCollection` singletons (including generated roots) from `ConfigureEndpointCollection` and pair with typed `PageCollection` singletons. Never cache request URLs or auth results statically. `EndpointName` is a label, not a registered name. Use `Path(...)` for templates and `Url.Endpoint` for routing, constraints, encoding, and prefixes.

Hosting ships `net10.0` generation via buildTransitive targets (README **Automatic hosted accessors** owns eligibility, `DRNHOSTING002`, and Razor rules). Reuse typed singletons and keep `Get` and route constants handwritten. Controller accessors require `ControllerBase`. Discovery skips others and explicit mappings report `DRNHOSTING001`.

Prefer MVC routing or `Url.Endpoint(endpoint, values)` for APIs. Use `~/` for HTML/HTMX URLs, `Url.Content` in C#, and `Request.ApplicationUrl` for configured destinations (such as MFA pages). Preserve validated return URLs. Use `ForPage` for page identities and `ForUrl` for URLs ([Razor accessors](../frontend-razor-accessors/SKILL.md)).

`IPageUtils.RenderPageAsync` supports `_ViewStart`, layouts, and links for unmatched requests, disposing its DI scope asynchronously on success or failure. `parentContext` preserves `PathBase` and nonce without transferring scope ownership.

## Error Responses

DRN exceptions retain status (default 500). Flurl keeps 400-499/503/504 and maps other failures to 502. `BadHttpRequestException` keeps `StatusCode` (default 400). Other exceptions return 500. Names and inner exceptions never choose status. Preserve cancellation/abort behavior and never replace a started response.

Problem `Type` precedence: response, scope, status default, `about:blank` (never changes status). Leave `Type` unset for routine failures. Explicit types name stable public conditions, never CLR names, routes, or request data.

API 400-599 responses emit Problem Details in every environment with `JsonConventions.DefaultOptions` (not MVC/HTTP JSON overrides). Matched endpoints use API metadata. Unmatched routes/405s use `DrnApiErrorOptions.PathPrefixes` (default empty). Preserve auth decisions, headers, cookies, validation, and extensions. `requestId` is `HttpContext.TraceIdentifier`. Default `instance` is `PathBase + Path` without query. Keep exceptions and fallbacks private/redacted.

`ScopeContext.Problem` fills missing fields/keys without overriding standard fields or validation. `[DisableDrnProblemDetails]` opts out of formatting and Swagger error metadata while keeping native responses and auth (README **API Failure Responses**).

`HandleHtmxRequests` defaults false. Opt in for HTMX Problem Details (including Razor handlers) while preserving redirects, auth, opt-outs, and custom non-problem bodies. Razor `Page()` selects HTML. `ObjectResult` with `ValidationProblemDetails(ModelState)` selects JSON at 400/422. Use safe messages, exact posted field names, and an empty key for form-level errors without overwriting API keys (README **HTMX Partial Requests**, [browser handling](../drn-buildwww-libraries/SKILL.md#problem-details-consumer)).

`DrnPageOptions` configures anonymous, non-cacheable 400/404/5xx browser pages independent of app data while preserving status, request reference, prefixes, redaction, existing bodies, Development diagnostics, and unmatched-route fallback auth. Supported API exceptions bypass `WriteProductionErrorResponseAsync` unless opted out. For access-denied references, use `AccessDeniedPageModel` and `PreserveAccessDeniedRequestId()` (README **Custom Error Pages** owns cookie-event ordering, TempData, and `EventsType`).

Follow [Utils](../drn-utils/SKILL.md) and the profile diagnostic guide. Apps own dynamic classification. `SanitizeText` does not certify content and runtime names do not redact. Keep private diagnostics out of public errors. Use `[DeveloperExceptionPreview]` on preview endpoints and `DeveloperExceptionAccess.AllowsAsync` for links (README **Developer Diagnostics** and **Startup Exception Reports**).

## Rate Limiting Rules

README **Rate Limiting** and **Telemetry** own settings, examples, metrics, and log fields.

- Derive from `SingletonRateLimitRule`/`ScopedRateLimitRule` for DI. Return null when inapplicable, or a `RateLimitRuleResult` quota, custom partition, allow, or deny result.
- Lower `Order` runs first and quotas compose. `Action` and `StopRemainingRules` are distinct. `ShortCircuitOnMatch` runs first at the same order and stops later rules on match. Put bypass rules before quotas. Earlier quotas and native policies still apply.
- `[DisableRateLimiting]` bypasses pre-auth quota (`[EnableRateLimiting]` does not). `PolicyName` targets matching policies (null global, blank invalid). Post-auth keeps DI-configured policies and rejection callbacks.
- Defaults: post-auth 250/min, pre-auth IP 2,500/min (`DrnAppFeatures:DrnRateLimit` startup snapshot, zero inherits shared settings). App rules own tenant/account/endpoint quotas.
- Use stable user IDs plus scheme, not display names. Scoped rules use `IScopedUser`/`RateLimitFor`. Preload policy data for sync evaluation. Pass immutable values to partition factories, never captured `HttpContext` or scoped services.
- Rejected IP/partition fields log keyed hashes (`PlainText` only for controlled dev/encrypted audit sinks). State is process-local. Hard multi-replica quotas require edge or custom distributed enforcement, not `HybridCache` alone.

## TagHelpers

Activate in `_ViewImports.cshtml`:

```razor
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, DRN.Framework.Hosting
```

README **Razor TagHelpers** owns Vite/SRI, CSP nonces, HTMX CSRF, active links, opt-outs, and script defaults. Buildwww skills own asset layout.

`authorized-only`/`anonymous-only` are presence-only markers checking `ScopeContext.Authenticated` (even `"false"` enables filtering, omit or use Razor conditions to disable, does not check MFA). `policy-only="PolicyName"` checks the user with optional `policy-resource` and fails on invalid policy names. Visibility never inspects linked endpoints or replaces endpoint authorization.

```razor
<a asp-page="/User/Login" anonymous-only>Sign In</a>
<button type="button" policy-only="EditDocument" policy-resource="@Model.Document">Edit</button>
```

Publish Vite manifests with assets. Use `DrnStaticFileHeaderOptions` for startup header rules (`Set`, `Remove`, `Override`, `IfMissing`). `robots.txt` revalidation is enabled by default. README **Static Asset Compression** owns integrity hashes, Brotli-only entries, and path matching.

## Related Skills

- [Framework overview](../overview-drn-framework/SKILL.md): shared conventions.
- [EntityFramework](../drn-entityframework/SKILL.md): dependency launch.
- [Razor layout](../frontend-razor-pages-shared/SKILL.md): UI lifecycle.

[hosting]: https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Hosting/README.md
