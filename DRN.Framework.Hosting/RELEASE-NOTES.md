Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.9.5

### Breaking Changes

*   **Host Filtering Configuration**: `AllowedHosts` must now be configured outside Development and cannot contain `*`. Development still falls back to `*` for local convenience, but Staging and Production fail closed when host filtering is missing or wildcarded.

### Changed

*   **Razor Development Workflow**: Removed the default `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` dependency and `AddRazorRuntimeCompilation()` registration. DRN now relies on Razor SDK build-time/publish-time compilation and IDE or `dotnet watch` Hot Reload for local `.cshtml` iteration, following .NET 10 guidance that Razor runtime compilation is obsolete.
    *   References: [Razor runtime compilation is obsolete](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/razor-runtime-compilation-obsolete), [.NET Hot Reload support for ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/hot-reload).
*   **Production Error Responses**: Production exception responses and `ProblemDetails` no longer include raw exception messages or stack details. Development keeps detailed diagnostics.

### Bug Fixes

*   **Vite Manifest Integrity and Validation**: Vite manifest parsing now resolves from the web root, validates that manifest files and referenced assets stay under the expected output folders, fails fast on missing manifest assets, and emits SHA-256 integrity hashes in standard Base64 for browser SRI compatibility.

### New Features

*   **Dual-Layer Rate Limiting**: Added pre-auth and post-auth rate limiting with lifetime-specific `ISingletonRateLimitRule` / `IScopedRateLimitRule` support, safe partition-based rule results, and extensibility for tenant/user/IP policies.
    *   `SingletonRateLimitRule` and `ScopedRateLimitRule` now provide automatic attribute-based DI registration for derived rules; direct interface implementations can still opt into explicit DI attributes.
    *   Pre-auth rate limiting honors ASP.NET Core `[DisableRateLimiting]` endpoint metadata and keeps `[EnableRateLimiting]` aligned with ASP.NET Core global-limiter semantics.
    *   Default post-auth partitioning now uses stable user id claims (`NameIdentifier`/`sub`) with auth scheme instead of mutable display names.
    *   Matching rules compose through .NET's native chained limiter so tenant + user + IP policies can be enforced together.
    *   Scoped rules are post-auth only, preserve global ordering with singleton rules, compose together, and same-order rules can opt into `ShortCircuitOnMatch` for allow/deny precedence.
    *   Rule-level `PolicyName` filters DRN rules by ASP.NET Core `[EnableRateLimiting("policy-name")]` endpoint metadata without replacing native named policies.
    *   Added app-specific `RateLimitFor` pattern (e.g., `Sample.Hosted.Helpers.RateLimitFor`) for claim-based scoped partitions composed from `Get.Claim.*` primitives backed by cached `IScopedUser` claims.
    *   Post-auth rate limiting now preserves named policies and rejection callbacks configured through `AddRateLimiter(options => ...)`, so `[EnableRateLimiting("policy-name")]` works alongside DRN's global rule chain.
    *   DRN rule rejection attribution now tracks the rule that actually failed, so native named-policy rejections do not trigger unrelated DRN rule `OnRejectedAsync` callbacks.
    *   Hot-path rule selection uses value-based rule results/matches and cached default-rule option factories to reduce avoidable per-request allocation pressure.
    *   Added `RateLimitRuleResult.DenyRequest(...)` and explicit `RateLimitRuleAction` values for immediate 429 denials, keeping allow, deny, quota, and short-circuit semantics separate and testable.
    *   Added `DRN.Framework.Hosting.RateLimiting` metrics for OpenTelemetry exports, including pre-auth lease metrics, DRN rule-level rejection counters, and an `action` tag for `limit` / `allow` / `deny` visibility.
    *   Pre-auth and post-auth rejection logging now use `DrnRateLimit.PartitionLogMode`, defaulting to deterministic keyed hashes for correlation without raw API-key, tenant-hint, service-id, user-id, or IP leakage. `PlainText` can be enabled explicitly for controlled development or dedicated audit sinks.
    *   Pre-auth and post-auth token bucket settings can now diverge via phase-specific `DrnAppFeatures` overrides; pre-auth defaults are intentionally coarser for B2B NAT/VPN/CDN egress addresses.
    *   Production docs clarify rate limit settings, endpoint metadata usage, reference links, dynamic tenant-plan guidance, and that built-in limiter state is process-local and should be paired with edge or Redis-backed distributed limiting for horizontally scaled enforcement.
*   **Vite Manifest Publish Support**: Added a transitive MSBuild target that includes `wwwroot/**/.vite/manifest.json` in Web SDK publish output so published applications preserve Vite manifest lookup, SRI generation, and static asset pre-warming. Set `DrnHostingViteManifestPublishItemsEnabled=false` to opt out.

## Version 0.9.4

Dependencies upgraded to dotnet 10.0.8

## Version 0.9.3

Dependencies upgraded to dotnet 10.0.7

## Version 0.9.2

Dependencies upgraded to dotnet 10.0.6

## Version 0.9.1

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and is proud to inherit his spiritual legacy: 'I am not leaving behind any definitive text, any dogma, any frozen, rigid rule as my spiritual legacy. My spiritual wealth is science and reason. Those who wish to embrace me after my death will become my spiritual heirs if they accept the guidance of reason and science on this fundamental axis.'

### New Features

*   **Composable Builder Configuration**: Extracted `ConfigureLoggingBuilder` and `ConfigureWebHostBuilder` as `protected virtual` methods from `ConfigureApplicationBuilder` for independent subclass customization.

## Version 0.9.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and stands behind his remarkable words: 'Peace at home, peace in the world.'

## Version 0.8.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals, rooted in his timeless words that 'science is the truest guide in life.' In that spirit, and to honor the 14 March Scientists Day, this release is dedicated to the researchers working for the benefit of humanity, and to the rejection of my first academic paper :) ([JOSS #10176](https://github.com/openjournals/joss-reviews/issues/10176)).

### New Features

*   **ApplicationLifetime Shutdown Hook**: `DrnProgramBase` now registers `IHostApplicationLifetime.StopApplication` as `ApplicationLifetime.ShutdownAction` during application bootstrap. This enables `TimeStampManager`'s clock drift handler to trigger graceful application shutdown when critical drift is detected.

## Version 0.7.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals and honors 8 March, International Women's Day, a cause inseparable from his vision of equality. This release is dedicated to freedom of speech, democracy, women's rights, and Prof. Dr. Ümit Özdağ, a defender of Mustafa Kemal Atatürk’s enlightenment ideals.

> [!WARNING]
> Since v0.6.0 (released 10 November 2024), substantial changes have occurred. This release notes file has been reset to reflect the current state of the project as of 08 March 2026. Previous history has been archived to maintain a clean source of truth based on the current codebase.

### New Features

*   **Security First Architecture**
    *   **Fail-Closed MFA**: `MfaEnforcingAuthorizationPolicyProvider` enforces Multi-Factor Authentication by default. Opt-out via `[AllowAnonymous]` or `[Authorize(Policy = AuthPolicy.MfaExempt)]`.
    *   **Strict CSP & Nonce**: Content Security Policy with automatic nonce generation for all scripts and styles.
    *   **Security & GDPR Headers**: Automatic injection of `HSTS`, `FrameOptions`, `ContentTypeOptions`, and `SameSite=Strict`/`HttpOnly` cookies.
    *   **MFA Hooks**: `ConfigureMFARedirection` and `ConfigureMFAExemption` for customizing authentication flow.
*   **DrnProgramBase Lifecycle Hooks**
    *   **Builder Phase**:
        *   `ConfigureSwaggerOptions`: Customize OpenAPI metadata.
        *   `ConfigureDefaultSecurityHeaders` / `ConfigureDefaultCsp`: Define security policies.
        *   `ConfigureMvcBuilder` / `ConfigureMvcOptions`: Customize MVC conventions and Razor Pages options.
        *   `ConfigureStaticFileOptions` / `ConfigureResponseCachingOptions`: Optimize asset delivery with server-side response caching (16MB max, case-insensitive) and automatic static asset caching.
        *   `ConfigureResponseCompressionOptions` / `ConfigureCompressionProviders`: Brotli and Gzip compression for static assets with built-in BREACH/CRIME protection.
        *   `ConfigureCookiePolicy`: Centralized security settings for cookies (HttpOnly, Secure, SameSite) with environment-aware defaults via `IsDevelopmentEnvironment`.
    *   **Pipeline Phase**:
        *   `ConfigureApplicationPipelineStart`: HSTS, Forwarded Headers.
        *   `ConfigureApplicationPreScopeStart`: Static files, caching, and compression.
        *   `ConfigureApplicationPreAuthentication` / `PostAuthentication`: Localization, MFA logic.
        *   `MapApplicationEndpoints`: Route mapping.
    *   **DrnProgramActions**: "Hook Method" pattern for intercepting startup (`ApplicationBuilderCreatedAsync`, `ApplicationBuiltAsync`, `ApplicationValidatedAsync`) without modifying Program.cs.
*   **Type-Safe Routing**
    *   **EndpointCollectionBase**: Strongly-typed API accessors (e.g., `Get.Endpoint.User.Login.Path()`).
    *   **PageCollectionBase**: Type-safe Razor Page navigation (e.g., `Get.Page.User.Profile`).
    *   **Validation**: `ValidateEndpoints` ensures all typed routes match actual mapped endpoints at startup.
*   **Frontend Integration & TagHelpers**
    *   **Asset Management**: `ViteScriptTagHelper` and `ViteLinkTagHelper` for resolving manifest-based assets with integrity checks.
    *   **Security**: `NonceTagHelper` (auto-injects CSP nonce) and `CsrfTokenTagHelper` (auto-injects token for HTMX).
    *   **Conditional Rendering**: `AuthorizedOnlyTagHelper` (MFA-aware) and `AnonymousOnlyTagHelper`.
    *   **Navigation**: `PageAnchorAspPageTagHelper` and `PageAnchorHrefTagHelper` automatically mark active links.
    *   **Modern Defaults**: `ScriptDefaultsTagHelper` applies `defer` for external scripts and `type="module"` for inline scripts by default, with explicit opt-out support.
*   **Advanced Middleware & HTTP Standards**
    *   **Standardized Redirects**: Automatically converts 302 (Found) to 303 (See Other) for modern HTTP/1.1 POST response compliance.
    *   **Security-First Headers**: Default `Cache-Control: no-store` and strictly configured HSTS/CSP/Nonce headers.
    *   **Malicious Request Detection**: Automatically aborts requests to protected developer URIs or suspicious paths.
    *   **Flurl Resilience**: Integrated mapping of `FlurlHttpException` to standard gateway status codes.
*   **Developer Diagnostics**
    *   **Startup Exception Reports**: Generates detailed `StartupExceptionReport.html` if the application fails during initialization (Development only).
    *   **Enhanced Error Pages**: Custom `RuntimeExceptionPage` and `CompilationExceptionPage` with stack trace analysis and model capture.
    *   **Diagnostic Events**: Built-in integration with `DiagnosticSource` for unhandled exception tracking.
*   **Identity & GDPR Consent**
    *   **Consent Integration**: Automatic extraction and propagation of `ConsentCookie` model via `ScopedUserMiddleware`.
    *   **Identity Helpers**: `IdentityApiHelper` for standardized validation problem reporting.
*   **Static Asset Pre-Warming**
    *   **`StaticAssetWarmService`**: `[HostedService]` that populates `ResponseCaching` with Brotli and Gzip compressed Vite manifest assets at startup — zero compression latency for end users.
    *   **Compression**: `SmallestSize` (maximum) for both Brotli (Level 11) and Gzip by default, overrideable via `ConfigureBrotliCompressionLevel()` / `ConfigureGzipCompressionLevel()`.
*   **Infrastructure & Development**
    *   **`IAppStartupStatus`**: Singleton gate for background services to await full host startup before executing.
    *   **`IServerSettings`**: Resolves bound Kestrel addresses with wildcard-to-localhost normalization for internal self-requests.
    *   **Local Provisioning**: `LaunchExternalDependenciesAsync` auto-starts PostgreSQL Testcontainers in Debug mode; RabbitMQ is available through the explicit testing helper.
    *   **Validation**: `ValidateEndpoints` and `ValidateServicesAddedByAttributesAsync` ensure system integrity at startup.
    *   **Identity Integration**: `IdentityControllerBase` and `ScopedUserMiddleware` for deep identity context propagation.

---

Documented with the assistance of [DiSCOS](https://github.com/duranserkan/DRN-Project/blob/develop/DiSCOS/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
