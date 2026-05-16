---
name: drn-hosting
description: DRN.Framework.Hosting - DrnProgramBase for web application bootstrapping, endpoint configuration, security middleware (CSP, nonce), authentication/authorization, TagHelpers for asset management, and Razor Pages integration. Essential for web application setup and hosting. Keywords: hosting, web-application, drnprogrambase, endpoints, middleware, security, csp, nonce, authentication, authorization, taghelpers, razor-pages, mfa, background-service
last-updated: 2026-05-16
difficulty: advanced
tokens: ~3K
---

# DRN.Framework.Hosting

> Web application hosting with security-first design, endpoints, and middlewares.

## When to Apply
- Creating new hosted applications
- Configuring security (CSP, Auth, MFA)
- Working with endpoints and Razor Pages
- Adding or customizing middlewares
- Using TagHelpers for frontend rendering

---

## DrnProgramBase Pattern

All DRN web apps inherit from `DrnProgramBase<TProgram>`:

```csharp
public class SampleProgram : DrnProgramBase<SampleProgram>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(
        WebApplicationBuilder builder, 
        IAppSettings appSettings, 
        IScopedLog scopedLog)
    {
        builder.Services.AddSampleHostedServices(appSettings);
        return Task.CompletedTask;
    }
    
    protected override MfaRedirectionConfig ConfigureMFARedirection()
        => new(Get.Page.User.Management.EnableAuthenticator, 
               Get.Page.User.LoginWith2Fa,
               Get.Page.User.Login, 
               Get.Page.User.Logout, 
               Get.Page.All);
}
```

### Builder Phase Hooks

| Method | Purpose |
|--------|---------|
| `AddServicesAsync()` | **[Required]** Add services to DI |
| `ConfigureSwaggerOptions()` | Customize Swagger/OpenAPI |
| `ConfigureApplicationBuilder()` | Root builder customization |
| `ConfigureMvcBuilder()` | IMvcBuilder customization |
| `ConfigureDefaultSecurityHeaders()` | CSP and security header policies |
| `ConfigureDefaultCsp()` | Customize CSP directives |
| `ConfigureSecurityHeaderPolicyBuilder()` | Route-specific security policies |
| `ConfigureAuthorizationOptions()` | Authorization policy config |
| `ConfigureCookiePolicy()` | GDPR and consent cookie settings |
| `ConfigureStaticFileOptions()` | Static file serving and caching |
| `ConfigureResponseCachingOptions()` | Response caching (16MB limit, case insensitive) |
| `ConfigureResponseCompressionOptions()` | Compression MIME types, HTTPS=false (BREACH prevention) |
| `ConfigureCompressionProviders()` | Brotli + Gzip provider setup |
| `ConfigureBrotliCompressionLevel()` | Brotli level (default: SmallestSize) |
| `ConfigureGzipCompressionLevel()` | Gzip level (default: SmallestSize) |
| `ConfigureMvcOptions()` | MvcOptions configuration |
| `ConfigureForwardedHeadersOptions()` | Forwarded headers (default: All) |
| `ConfigureRequestLocalizationOptions()` | Localization cultures, cookie provider |
| `ConfigureHostFilteringOptions()` | Allowed hosts from config |
| `ConfigureSecurityStampValidatorOptions()` | Security stamp refresh with AMR claim preservation |
| `ConfigureDefaultCspBase()` | Base CSP directives (base-uri, form-action, frame-ancestors) |
| `ConfigureCookieTempDataProvider()` | TempData cookie settings |
| `CreatePreAuthRateLimiter()` | Pre-auth rate limiter orchestration |
| `ConfigurePostAuthRateLimiterOptions()` | Post-auth rate limiter orchestration and policies |

### Application Phase Hooks

| Method | Purpose |
|--------|---------|
| `ConfigureApplicationPipelineStart()` | Earliest middleware (HSTS, Security Headers) |
| `ConfigureApplicationPreScopeStart()` | Pre-logger (Static files) |
| `ConfigureApplicationPostScopeStart()` | After HttpScopeMiddleware |
| `ConfigureApplicationPreAuthentication()` | Before Auth (Localization) |
| `ConfigureApplicationPostAuthentication()` | Post-Auth (MFA Redirection) |
| `ConfigureApplicationPostAuthorization()` | Post-AuthZ (Swagger UI) |
| `MapApplicationEndpoints()` | Route mapping (Controllers, Razor Pages) |
| `ValidateEndpoints()` | Post-mapping endpoint validation |
| `ValidateServicesAsync()` | DI validation |
| `ConfigureMFARedirection()` | MFA page configuration |
| `ConfigureMFAExemption()` | Route-specific MFA exemption config |

**Execution order**: Builder Phase → `builder.Build()` → Pipeline Phase → ValidateEndpoints → ValidateServices → `application.RunAsync()`

### Advanced Startup (`DrnProgramActions`)

Intercept startup without modifying main program class:

```csharp
public class SampleProgramActions : DrnProgramActions
{
    public override async Task ApplicationBuilderCreatedAsync<TProgram>(
        TProgram program, WebApplicationBuilder builder,
        IAppSettings appSettings, IScopedLog scopedLog)
    {
        // Hook into builder creation (e.g., launch containers)
    }

    public override async Task ApplicationValidatedAsync<TProgram>(
        TProgram program, WebApplication application,
        IAppSettings appSettings, IScopedLog scopedLog)
    {
        // Hook after DRN validations (e.g., seed data)
    }
}
```

### Configuration Properties

| Property | Description |
|----------|-------------|
| `AppBuilderType` | Controls builder creation (Empty, Slim, Default, DrnDefaults) |
| `DrnProgramSwaggerOptions` | OpenAPI and Swagger UI config |

---

## Security Features

### MFA by Default

MFA enforced globally via `FallbackPolicy`. Any route not opted-out requires MFA.

```csharp
// Opt-out options:
[AllowAnonymous]                            // Fully anonymous
[Authorize(Policy = AuthPolicy.MfaExempt)]  // Single-factor only

// Disable MFA globally:
protected override void ConfigureAuthorizationOptions(AuthorizationOptions options) { }
```

### GDPR & Consent

- `ConsentCookie`: Manages user consent state via secure HttpOnly cookie
- `ScopedUserMiddleware`: Populates `IScopedLog` with `ConsentGranted` status

### Per-Route Security Headers

```csharp
protected override void ConfigureSecurityHeaderPolicyBuilder(HeaderPolicyCollection policies, IAppSettings appSettings)
{
    policies.AddPolicy("AllowExternalScripts", builder =>
        builder.AddContentSecurityPolicy(csp =>
            csp.AddScriptSrc().Self().From("https://cdn.example.com")));
}
```

---

## Page & Endpoint Management

### PageCollectionBase

```csharp
public class SamplePageFor : PageCollectionBase<SamplePageFor>
{
    public RootPageFor Root { get; } = new();
    public UserPageFor User { get; } = new();
}

public class UserPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["User"];
    public string Login { get; init; } = string.Empty;    // "/User/Login"
    public string Register { get; init; } = string.Empty;  // "/User/Register"
    public UserProfilePageFor Profile { get; } = new();
}
```

```razor
<a asp-page="@Get.Page.User.Login">Log In</a>
```

### EndpointCollectionBase

```csharp
public class SampleEndpointFor : EndpointCollectionBase<SampleProgram>
{
    public QaApiFor Qa { get; } = new();
}

public class TagFor() : ControllerForBase<TagController>(QaApiFor.ControllerRouteTemplate)
{
    public ApiEndpoint GetAsync { get; private set; } = null!;
    public ApiEndpoint PostAsync { get; private set; } = null!;
}

// Usage: Get.Endpoint.Qa.Tag.GetAsync.Path()
```

---

## Middlewares

| Middleware | Purpose |
|------------|---------|
| `HttpScopeMiddleware` | Request/response logging with IScopedLog, TraceId, duration |
| `PreAuthRateLimitingMiddleware` | Early abuse throttling before authentication |
| `ScopedUserMiddleware` | Populates IScopedLog with user identity and consent |
| `MfaRedirectionMiddleware` | Redirect users without MFA to setup page |
| `MfaExemptionMiddleware` | Exempt specific routes/schemes from MFA |

### Rate Limiting Rules

- Derive from `SingletonRateLimitRule` / `ScopedRateLimitRule` for automatic attribute-based DI registration.
- Return `null` when the rule does not apply; return `RateLimitRuleResult.TokenBucket(...)`, `FixedWindow(...)`, `SlidingWindow(...)`, `ConcurrencyLimiter(...)`, `CustomPartition(...)`, or `AllowRequest(...)` when it applies.
- Lower `Order` runs first. Matching rules compose through .NET's native chained limiter, so tenant + user + IP can all apply; framework defaults use `int.MaxValue`.
- Override `ShortCircuitOnMatch` for same-order allow/deny rules that must run before normal same-order quota rules; use lower `Order` when they must bypass earlier singleton or scoped quotas. If they return `null`, later rules still evaluate.
- The pre-auth middleware honors ASP.NET Core `[DisableRateLimiting]` endpoint metadata; use it for trusted health checks or operational endpoints that must never consume quota. `[EnableRateLimiting]` does not bypass the global pre-auth limiter.
- Default post-auth partitioning uses stable user id claims (`NameIdentifier`/`sub`) with auth scheme, not mutable display names.
- Use scoped rules plus `IScopedUser` for post-auth claim-aware partitions. Prefer `RateLimitFor` (or app-owned wrappers around `RateLimitFor`) over repeated `HttpContext.User` parsing.
- Set `PolicyName` on a rule only when it should run for endpoints marked with matching ASP.NET Core `[EnableRateLimiting("policy-name")]` metadata. `null` means global DRN rule; blank names are invalid.
- Post-auth defaults to 100/minute; pre-auth defaults to a coarser 1,000/minute IP bucket for B2B NAT/VPN/CDN egress addresses. Configure settings under `DrnAppFeatures:DrnRateLimit`; phase override values of 0 inherit the shared settings.
- Singleton rules are sorted once. Pre-auth uses singleton rules only. Scoped rule existence/order is detected at startup, then scoped rules are resolved from the request provider only for post-auth. Global `Order` is preserved across singleton and scoped rules; same-order `ShortCircuitOnMatch` rules run first, and every matching rule composes. Limiter partition factories must not capture `HttpContext` or scoped services because limiter instances are cached per partition.
- Post-auth uses DI-configured `RateLimiterOptions`, so named policies and rejection callbacks registered through `AddRateLimiter(options => ...)` remain available to `[EnableRateLimiting("policy-name")]`.
- DRN emits metrics through the `DRN.Framework.Hosting.RateLimiting` meter; add this meter to OpenTelemetry exports when pre-auth metrics or DRN rule-level rejection metrics are needed.
- Default limiter state is process-local. For horizontally scaled production enforcement, pair DRN app-local limits with edge or distributed rate limiting.

---

## Background Services

Use `[HostedService]` attribute to auto-register `BackgroundService` implementations:

```csharp
[HostedService]
public class MyBackgroundWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
    }
}
```

---

## TagHelpers

| TagHelper | Target | Purpose |
|-----------|--------|---------|
| `ViteScriptTagHelper` | `<script>` | Resolve Vite manifest + SRI |
| `ViteLinkTagHelper` | `<link>` | Resolve Vite manifest + SRI |
| `NonceTagHelper` | `<script>`, `<style>`, `<link>`, `<iframe>` | Add CSP nonce |
| `CsrfTokenTagHelper` | `hx-post/put/delete/patch` | Add CSRF token |
| `AuthorizedOnlyTagHelper` | `*[authorized-only]` | Render if MFA complete |
| `AnonymousOnlyTagHelper` | `*[anonymous-only]` | Render if anonymous |
| `PageAnchorAspPageTagHelper` | `<a asp-page>` | Mark active page |
| `PageAnchorHrefTagHelper` | `<a href>` | Mark active page |
| `ScriptDefaultsTagHelper` | `<script>` | Modern defaults: `defer` (external), `type="module"` (inline) |

**Vite**: `<script src="buildwww/app/js/appPostload.js">` → `<script src="/app/app_postload.abc123.js" integrity="sha256-xyz">`

**Nonce**: Auto-added to `<script>`, `<style>`, `<link>`, `<iframe>`. Opt-out: `<script disable-nonce="true">`

**CSRF**: Auto-added to `hx-post/put/delete/patch`. Opt-out: `<button disable-csrf-token="true">`

**Auth visibility**:
```razor
<nav authorized-only>Profile links here</nav>
<a asp-page="/User/Login" anonymous-only>Sign In</a>
```

**Active page marking**:
```razor
<a asp-page="/Dashboard">Dashboard</a>
<!-- If on /Dashboard → class="active fw-bold" aria-current="page" -->
<a asp-page="/Settings" ActiveClass="current">Settings</a>
<a asp-page="/Help" MarkWhenActive="false">Help</a>
```

---

## Configuration (appsettings.json)

### Kestrel

```json
{
  "Kestrel": {
    "EndpointDefaults": { "Protocols": "Http1" },
    "Endpoints": { "All": { "Url": "http://*:5988" } }
  }
}
```

### NLog

```json
{
  "NLog": {
    "targets": {
      "console": { "type": "Console", "layout": "${longdate}|${level}|${logger}|${message}${onexception:|${exception:format=tostring}}" }
    },
    "rules": [
      { "logger": "*", "minLevel": "Info", "writeTo": "console" },
      { "logger": "Microsoft.*", "maxLevel": "Info", "final": true },
      { "logger": "Microsoft.Hosting.Lifetime", "minLevel": "Info", "writeTo": "console", "final": true }
    ]
  }
}
```

### wwwroot Structure

**Application (`*.Hosted/wwwroot/`)** — Vite build output:
```
wwwroot/
├── app/           # Vite-built JS (app_preload.[hash].js, app_postload.[hash].js)
├── images/        # Static images
└── lib/           # Third-party bundles (bootstrap, htmx, bootstrap-icons, onmount)
```

**Vite source (`*.Hosted/buildwww/`)** — unbundled source files:
```
buildwww/
├── app/
│   ├── css/       # App stylesheets
│   └── js/        # App scripts (appPreload.js, appPostload.js)
├── lib/           # Library sources (bootstrap scss, htmx bundle)
├── plugins/       # Vite plugins
└── types/         # TypeScript type definitions
```

> `vite.config.js` defines named builds (`app`, `htmx`, `bootstrap`) selected via `BUILD_TYPE` env var. Output goes to `wwwroot/` with content-hashed filenames and manifest for TagHelper resolution.

> See [drn-entityframework](../drn-entityframework/SKILL.md) for `LaunchExternalDependenciesAsync` setup with Testcontainers.

---

## Related Skills

- [overview-drn-framework.md](../overview-drn-framework/SKILL.md) - Framework overview
- [drn-utils.md](../drn-utils/SKILL.md) - Utils and DI
- [frontend-razor-pages-shared.md](../frontend-razor-pages-shared/SKILL.md) - Layout system
- [frontend-razor-accessors.md](../frontend-razor-accessors/SKILL.md) - Accessor patterns

---

## Global Usings

```csharp
global using DRN.Framework.SharedKernel;
global using DRN.Framework.SharedKernel.Domain;
global using DRN.Framework.Utils.DependencyInjection;
global using DRN.Framework.Hosting.DrnProgram;
global using DRN.Framework.Hosting.Endpoints;
global using Microsoft.AspNetCore.Mvc.RazorPages;
```
