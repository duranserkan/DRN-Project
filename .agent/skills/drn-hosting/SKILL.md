---
name: drn-hosting
description: "Configure DRN hosts, MFA, API errors, rate limits, Razor endpoints, CSP and TagHelpers."
last-updated: 2026-11-21
difficulty: advanced
---

# DRN.Framework.Hosting

Use [version-matched references](../overview-drn-framework/SKILL.md#documentation-and-source-references). The [Hosting README][hosting] owns signatures, examples, defaults, and migration. Development guidance may be unreleased. `Example*` names are placeholders.

## DrnProgramBase Pattern

Derive from `DrnProgramBase<TProgram>`, implement `IDrnProgram` and `AddServicesAsync`. Use public/protected hooks, not internal configurators. README **Lifecycle & Execution Flow**, **DrnProgramBase Deep Dive**, and **QuickStart** own timing, base-call rules, and global usings. `DrnProgramActions` exposes `ApplicationBuilderCreatedAsync` and `ApplicationValidatedAsync`.

Configure Source-Known epoch/minimum before startup or first ID/epoch use. Temporary hosts and `SkipValidation` do not bypass time validation. Temporary apps skip endpoint validation/accessor population, run service validation and `ApplicationValidatedAsync`, and return without starting. Use explicit `DrnDevelopmentSettings` test flags. Never weaken runtime checks with environment branches. See [testing](../drn-testing/SKILL.md).

Razor uses IDE/`dotnet watch` Hot Reload, not runtime compilation. Execution needs authorization. Framework overview and Utils own configuration, DI, app data, environment conventions, and `[HostedService]` registration.

## Security Features

### MFA by Default

MFA applies by default, including role and named-policy endpoints. `[AllowAnonymous]` permits public access. `AuthPolicy.MfaExempt` permits authenticated access without MFA.

- Register an effective default or policy-selected authentication scheme. Exemption allowlists do not select schemes.
- Identity apps register `AddSignInManager<DrnSignInManager<TUser>>()` and `AddDrnIdentityMfaPolicies()`. Use `ConfigureAuthenticationClaims()` only for custom providers.
- `ConfigureMFARedirection()` supplies local setup/challenge pages (`null` disables redirection).
- Retain base Identity renewal callbacks. Refresh is not fresh MFA.
- External providers need validated handlers. Shared MFA does not require Identity.

README **MFA** owns assurance, renewal, revocation, audit events, and replacing default/fallback policies while keeping registered MFA policies.

### GDPR & Consent

`DrnDefaults` withholds non-essential cookies until consent. Apps own script activation, category preferences, and `IsEssential`. README **Cookies and consent** owns cookie settings and exposed consent state.

### Swagger And Developer Tool Authorization

Swagger is Development-only by default. To enable elsewhere, call base in `ConfigureSwaggerOptions`, set `AddSwagger = true`, and assign a named `AuthorizationPolicy` protecting UI, assets, and OpenAPI documents (including custom routes). MFA still applies. Null uses fallback authorization. Blank policy names are invalid.

Swagger UI prefixes use nonempty ASCII letter, digit, hyphen or underscore segments. Root mounts are rejected. Reserve the UI subtree for Swagger CSP. Disabled UI retains app policies. Retain base behavior in `ConfigureSecurityHeaderPolicyBuilder`. [Hosting README][hosting] owns `AddSwaggerUI`, metadata, document URLs, title defaults, caching, and `DrnPageOptions.FaviconUrl`.

## Page & Endpoint Management

README **Source-Known Controllers** owns single-tier service-backed `SourceKnown*Controller` (`ISourceKnownService`, `abstract ToResponse`, constructor `Dto` rejection guard throwing `InvalidOperationException`, virtual `CreateEntityAsync` delegation). See `CONVENTIONS.md#terminology` for canonical Domain, Subdomain, project stem, and role stem definitions. `DRNAUTHORIZATION001` requires named policies. SPA APIs use bearer-only schemes. Cookie APIs need antiforgery. Keep resource checks in app code. Disable actions with `[NonAction]` and omit accessors. `IgnoreApi` only hides docs.

Use typed `PageCollection`/`EndpointCollection` singletons and return the latter from `ConfigureEndpointCollection`, including generated roots. Never cache request URLs or authorization results statically. `EndpointName` is a label, not a registered name. `Path(...)` formats templates. Use `Url.Endpoint` for routing, constraints, encoding, and prefixes. README owns base-type migration, typed-link restrictions, and disposal exclusions.

Hosting ships `net10.0` generation via buildTransitive/shared targets. README **Automatic hosted accessors** owns eligibility, `DRNHOSTING002` naming, and Razor conventions. Reuse typed singletons and keep `Get` and route constants handwritten. Controller accessors require `ControllerBase`. Discovery skips others and explicit mappings report `DRNHOSTING001`.

Prefer MVC routing or `Url.Endpoint(endpoint, values)` for APIs. Use `~/` for HTML/HTMX URLs, `Url.Content` in C#, and `Request.ApplicationUrl` for configured destinations like MFA pages. Preserve validated return URLs. Use `ForPage` for page identities and `ForUrl` for URLs. See [Razor accessors](../frontend-razor-accessors/SKILL.md).

`IPageUtils.RenderPageAsync` supports `_ViewStart`, layouts, and links for unmatched requests, asynchronously disposing its DI scope on success or failure. `parentContext` preserves `PathBase` and nonce without transferring scope ownership.

## Error Responses

DRN exceptions retain status (default 500). Flurl preserves upstream 400-499/503/504 and maps other failures to 502. `BadHttpRequestException` retains `StatusCode` (default 400). Other exceptions become 500. Names/inner exceptions do not choose status. Preserve cancellation/abort behavior and never replace a started response.

Problem Type precedence: response, scope, status default, `about:blank`. Type overrides never change status. Leave Type unset for routine failures. Explicit types identify stable public conditions beyond status, never CLR names, routes, or request data.

API statuses 400-599 use Problem Details in every environment with `JsonConventions.DefaultOptions` (not MVC/HTTP JSON overrides). Matched endpoints use API metadata. Unmatched routes/routing 405s use `DrnApiErrorOptions.PathPrefixes` (default empty). Preserve auth decisions, headers, cookies, validation, and extensions. `requestId` is `HttpContext.TraceIdentifier`. Default `instance` is `PathBase + Path` without query. Keep exceptions and fallbacks private/redacted.

`ScopeContext.Problem` fills missing fields/keys without overriding standard fields or validation. `[DisableDrnProblemDetails]` opts controllers/actions out of formatting and Swagger error metadata while keeping native responses/auth. README **API Failure Responses** owns callbacks and Swagger metadata.

`HandleHtmxRequests` defaults false. Opt in for HTMX Problem Details, including Razor handlers, while preserving redirects, auth, opt-outs, and custom non-problem bodies. Razor `Page()` selects HTML. `ObjectResult` with `ValidationProblemDetails(ModelState)` selects JSON at 400/422. Use safe messages, exact posted field names, and an empty key for form-level errors, preserving existing API keys. See README **HTMX Partial Requests** and [browser handling](../drn-buildwww-libraries/SKILL.md#problem-details-consumer).

`DrnPageOptions` configures anonymous, non-cacheable browser 400/404/5xx pages independent of app data. Preserve status, request reference, deployment prefixes, redaction, existing bodies, Development diagnostics, and unmatched-route fallback auth. Supported API exceptions bypass `WriteProductionErrorResponseAsync` unless opted out.

For access-denied references, use `AccessDeniedPageModel` and `PreserveAccessDeniedRequestId()`. Follow README **Custom Error Pages** for cookie-event ordering, callback preservation, single-use TempData, and `EventsType`.

Follow [Utils](../drn-utils/SKILL.md) and the profile's diagnostic guide. Apps own dynamic classification. `SanitizeText` does not certify content and runtime names do not redact. Keep private diagnostics out of public errors, including raw callbacks.

Use `[DeveloperExceptionPreview]` on preview endpoints and `DeveloperExceptionAccess.AllowsAsync` for links. See [Hosting README][hosting], Developer Diagnostics and Startup Exception Reports.

## Rate Limiting Rules

README **Rate Limiting** owns settings/examples. **Telemetry** owns metrics/log fields.

- Derive from `SingletonRateLimitRule`/`ScopedRateLimitRule` for automatic DI. Return null when inapplicable, or a `RateLimitRuleResult` quota, custom partition, allow, or deny result.
- Lower `Order` runs first and quotas compose. `Action` and `StopRemainingRules` are distinct. `ShortCircuitOnMatch` runs first at the same order and stops later rules on match. Place bypass rules before quotas. Earlier quotas and native policies still apply, and null permits later evaluation.
- `[DisableRateLimiting]` bypasses pre-auth quota. `[EnableRateLimiting]` does not. `PolicyName` targets matching named policies (null global, blank invalid). Post-auth retains DI-configured policies/rejection callbacks.
- Defaults: post-auth 250/minute, pre-auth IP 2,500/minute. `DrnAppFeatures:DrnRateLimit` is a startup snapshot. Zero phase values inherit shared settings. App rules own tenant/account/endpoint quotas.
- Use stable user IDs plus scheme, not display names. Scoped rules use `IScopedUser`/`RateLimitFor`. Preload policy data for sync evaluation. Partition factories receive immutable values, never captured HttpContext/scoped services.
- Rejected IP/partition fields use keyed hashes, not whole-log anonymization (PlainText is for controlled dev/encrypted audit sinks). State is process-local. Hard multi-replica quotas require edge/custom distributed enforcement, not HybridCache alone.

## TagHelpers

Activate in `_ViewImports.cshtml`:

```razor
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, DRN.Framework.Hosting
```

See README **Razor TagHelpers** for Vite/SRI, CSP nonces, HTMX CSRF, active links, opt-outs, and script defaults. Buildwww skills own asset layout.

`authorized-only`/`anonymous-only` are presence-only markers using `ScopeContext.Authenticated` (even `"false"` enables filtering). Omit the marker or use Razor conditions to disable it. They do not check completed MFA. `policy-only="PolicyName"` checks the current user with optional `policy-resource`. Invalid policy names fail. Visibility never inspects linked endpoints or replaces endpoint authorization.

```razor
<a asp-page="/User/Login" anonymous-only>Sign In</a>
<button type="button" policy-only="EditDocument" policy-resource="@Model.Document">Edit</button>
```

Publish Vite manifests with emitted assets. See README **Static Asset Compression** for build-time integrity hashes and Brotli-only entries.

Use `DrnStaticFileHeaderOptions` for startup header rules (`Set` adds/updates, `Remove` disables, `Override` replaces, `IfMissing` preserves). Robots.txt revalidation is enabled by default. README **Static Asset Compression** owns examples and path matching.

## Related Skills

- [Framework overview](../overview-drn-framework/SKILL.md): shared conventions.
- [EntityFramework](../drn-entityframework/SKILL.md): dependency launch.
- [Razor layout](../frontend-razor-pages-shared/SKILL.md): UI lifecycle.

[hosting]: https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Hosting/README.md
