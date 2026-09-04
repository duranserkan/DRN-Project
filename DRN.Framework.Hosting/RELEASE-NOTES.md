Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.10.0

### New Features

*   **Programmatic Application Builder Hook**: Added a four-argument `CreateApplicationAsync` overload for host integrations while retaining the existing three-argument overload for binary compatibility.
*   **Provider-Neutral MFA Claim Configuration**: Added `ConfigureMFAClaim` with the backward-compatible `MfaClaimConfig.AspNetIdentity` default (`amr=mfa`). Applications can select the exact claim type and value emitted by Keycloak or another identity provider without changing DRN authorization, redirection, or UI enforcement code.

### Breaking Changes

*   **Identity MFA Setup Response**: When MFA is globally enforced, password-valid accounts without two-factor authentication now receive an HTTP 200 five-minute setup credential instead of the ordinary authenticated credential returned previously. Cookie requests receive an empty response with a non-persistent, non-refreshable setup cookie; bearer requests receive an `AccessTokenResponse` with `ExpiresIn = 300` and an empty `RefreshToken`. Clients must use the setup credential with `TwoFactorAuth`, enable two-factor authentication, discard the setup credential, and log in again with an authenticator or recovery code.

### Operational Defaults

*   **Host-Level NLog Configuration**: `ConfigureLoggingBuilder` clears default logging providers and registers NLog only when the `NLog` configuration section exists in `IAppSettings`. Applications without an `NLog` section build hosts without NLog registration, while `RunAsync` still requires the section for bootstrap logging.

### Security

*   **Reverse Proxy Trust & Forwarded Headers**: `ConfigureForwardedHeadersOptions` automatically binds the `ForwardedHeaders` configuration section from `IAppSettings` with CIDR and `KnownProxies` support, throwing `ConfigurationException` on invalid IP network or proxy formats. When unconfigured, it defaults to trusting RFC 1918 private subnets (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`) alongside loopback and sets `ForwardLimit = 2` for secure IP resolution and rate limiting behind reverse proxies.
*   **Request Query Log Minimization**: `HttpScopeMiddleware` records only the query-parameter count instead of raw query strings, preventing credentials, tokens, and PII from entering structured logs.
*   **MFA Authorization & Identity Boundary Hardening**:
    *   **Bearer MFA Enforcement & Claim Preservation**: `IdentityLoginControllerBase.Refresh` validates the security stamp and preserves all `amr` (Authentication Method Reference) claims from authenticated identities across bearer token refreshes. For non-`amr` configured claim types, it preserves only the exact configured `MfaClaimConfig` (type/value pair) and rejects unrelated same-type claims from being copied from the refresh token.
    *   **MFA Setup Credential Isolation**: An `MfaSetupRequired` credential cannot satisfy an MFA requirement through either a configured authentication-scheme exemption or a simultaneously present completed-MFA claim.
    *   **Identity Management MFA Boundary**: MFA exemption moved from `IdentityManagementControllerBase` to its `TwoFactorAuth` endpoint. Initial enrollment accepts the short-lived `MfaSetupRequired` credential when MFA is enforced and an ordinary authenticated credential when MFA is optional. Once two-factor authentication is enabled, subsequent management requires completed MFA. `GetInfo`, `PostInfo`, and other identity management operations use the application's default or fallback policy and require completed MFA under the default configuration.
    *   **Login Failure Disclosure Minimization**: Login failures return the same generic unauthorized problem for unknown accounts, invalid passwords, and incomplete MFA, preventing account and enrollment-state disclosure through response details.
    *   **Authorization Metadata MFA Closure**: Global MFA enforcement is rechecked at the authorization middleware result boundary, preventing role-only and direct policy metadata from bypassing MFA while retaining explicit `MfaExempt` and configured authentication-scheme exemptions. Named policies also retain their configured authentication schemes when combined with the MFA default.
    *   **MFA Claim Authentication Identity Scoping**: `MfaAuthorization.IsMfaSatisfied` filters claims strictly to authenticated identities on the principal, preventing unauthenticated secondary identities from supplying completed MFA or setup claims.
    *   **Policy-Scheme Exemption Binding & Active Scheme Proof Discovery**: `MfaExemptionMiddleware` discovers the active configured exempt authentication scheme present on the request without suppression from ambient session state, short-circuiting on the first matching scheme and recording verified proof on `IScopedUser.ExemptionScheme` and `IScopedUser.ExemptionPrincipal`. `MfaAuthorization.IsMfaSatisfied`, `RequireMfaHandler`, and `MfaEnforcingAuthorizationMiddlewareResultHandler` bind exemptions strictly to the effective policy's authentication schemes, supporting claims transformations while eliminating claim-similarity heuristics and preventing non-selected credentials from waiving MFA on unrelated schemes.

## Version 0.9.8

Dependencies upgraded to dotnet 10.0.11

## Version 0.9.7

### Changed

*   **Response Compression MIME Registration**: Response compression middleware remains disabled over HTTPS for dynamic responses, while static files continue to opt in through `StaticFileOptions.HttpsCompression`. DRN's additional MIME registrations now contain only raw TTF/OTF font formats; CSS, JavaScript, SVG, and pre-compressed WOFF/WOFF2 formats are no longer added beyond ASP.NET Core defaults.
*   **Network Logging Dependency**: Upgraded `NLog.Targets.Network` from 6.0.4 to 6.1.4 in the published package dependency graph.

### Bug Fixes

*   **Asynchronous Hosting Resource Disposal**: Application shutdown and startup-exception reporting now asynchronously dispose the NLog provider and temporary service provider, allowing asynchronous cleanup to complete.
*   **Host-Aware Lifecycle Logging**: Successful application lifecycle logs now use the built host's configured logger, allowing test hosts and other consumers to suppress or redirect them. Failures before host construction continue to use the standalone bootstrap logger.

## Version 0.9.6

### Breaking Changes

*   **Environment Validation Requirement**: Application startup now fails fast with `ConfigurationException` if the `Environment` configuration key is missing, `NotDefined`, or set to an invalid value. Consumers must explicitly configure `Environment` (as `Development`, `Staging`, or `Production`) in `appsettings.json`, environment variables, mounted settings, or command-line arguments.

### Bug Fixes

*   **File Provider Preservation**: `AddDrnSettings` now preserves the outer builder's `IFileProvider` during environment resolution, ensuring custom or composite file providers are not discarded.
*   **Environment-Specific Configuration Discovery**: `AddDrnSettings` now discovers `Environment` without constructing full `AppSettings`, so `appsettings.{Environment}.json` can load even when required settings such as `NexusAppSettings` are supplied by the environment-specific file.

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

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/.agent/rules/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
