Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.11.0

### Breaking Changes

*   **Target framework**: Requires .NET 11 and ASP.NET Core 11.
*   **Program naming**: Every class deriving directly or indirectly from `DrnProgramBase<TProgram>` must end with the case-sensitive suffix `Program` (`DRNHOSTING002`). Rename nonconforming program classes.
*   **Typed endpoint and page accessors**: Replaced assembly-scanned `EndpointCollectionBase<TProgram>` roots with generated `<ProgramStem>EndpointFor` and `<ProgramStem>PageFor` roots under `<ProgramNamespace>.Helpers` (`App*` for `Program`). Update references to the generated roots, remove old root declarations and duplicate base/constructor/group definitions, move custom members into matching partial classes (using `AccessorController`/`AccessorGroup` for aliases and `AccessorMember(..., Exclude = true)` before replacing generated members), update `Get` facades, and return the generated singleton from `ConfigureEndpointCollection`. Replace static catalog properties with `IEndpointAccessor` and `EndpointSource` with `new DrnEndpointSource(accessor.EndpointHelper.EndpointDataSource)`.
*   **Development exception responses**: Detailed pages and their diagnostic JSON fallback require completed MFA and `SystemAdmin` or `TestPagesAccess=true`, honor stricter `DeveloperExceptionAccess.Policy` requirements, and send `Cache-Control: no-store`. Protect preview endpoints with `[DeveloperExceptionPreview]` and check links with `DeveloperExceptionAccess.AllowsAsync`.
*   **Diagnostic page routing**: `DeveloperView` paths no longer trigger diagnostics before authentication. Protect error preview pages with an authorization policy and replace `ExceptionPageAccessor.IsExceptionPage(path, isDevEnvironment)` with `IsExceptionPage(path)`.
*   **API failure responses**: API failures for 4xx and 5xx statuses now return `application/problem+json` with `requestId` in every environment using SharedKernel JSON conventions. Update clients that previously expected text, HTML, or empty bodies. Configure `DrnApiErrorOptions.PathPrefixes` for unmatched API routes. `WriteProductionErrorResponseAsync` handles browser, non-API, and opted-out API exceptions.
*   **Identity management API**: `IdentityManagementControllerBase` now uses API controller behavior. Add attribute routes to derived controllers. Invalid request models return validation Problem Details before the action runs, and cookie authentication failures return 401 or 403 instead of redirects.
*   **Runtime response compression removed**: Removed runtime response compression and `ConfigureResponseCompressionOptions`, `ConfigureCompressionProviders`, `ConfigureBrotliCompressionLevel`, and `ConfigureGzipCompressionLevel`. Remove overrides and precompress static assets in the frontend build.
*   **Vite manifests and compression**: Add a frontend build step writing each entry's uncompressed SHA-256 Base64 digest to `hash` (including for original-only deployments) and rebuild manifests. Removed startup pre-warming and `IViteManifest.WarmReport`. Entries marked `brotliOnly` require only `.br` files. Other entries require the original asset. Deploy manifests and assets without symbolic links below the web root.
*   **Swagger and OpenAPI defaults**: Serves OpenAPI 3.2 by default. Set `OpenApiVersion = OpenApiSpecVersion.OpenApi3_1` in `ConfigureSwaggerEndpointOptionsAction` if tooling requires 3.1. Without a named `AuthorizationPolicy`, Swagger UI now uses the fallback authorization policy. Browser-tab titles default to the API name and version, and UI prefixes accept only nonempty ASCII letter, digit, hyphen, or underscore segments.

### New Features

*   **Hosted accessor generation**: SDK Web hosts automatically generate `<ProgramStem>PageFor` and `<ProgramStem>EndpointFor` roots (`DRNHOSTING001`, `DRNHOSTING002`) from local controllers and Razor `@page` files, supporting `AccessorGroup`, `AccessorController` (including legacy `RouteTemplate` inference), `AccessorMember` aliases and exclusions, partial customization, and `[NonAction]` exclusions.
*   **Source-Known Controllers**: Added `SourceKnownControllerBase<TEntity, TResponse>`, `SourceKnownReadOnlyController<TEntity, TResponse>`, `SourceKnownController<TEntity, TResponse, TCreateRequest>`, and `SourceKnownWriteOnlyController<TEntity, TResponse, TCreateRequest>`. Controllers compose `ISourceKnownService`, keep `Repository` protected, delegate response projection through protected `CreateResponseAsync` and `CreateResponsesAsync` helpers, and delegate entity creation through virtual `CreateEntityAsync`.
*   **Static-file headers**: Added `DrnStaticFileHeaderOptions` for startup exact-path response header rules (`Set`, `Remove`, `Override`, `IfMissing`).
*   **Swagger options and authorization**: Added `DrnProgramSwaggerOptions.AuthorizationPolicy` for named-policy protection outside Development, `AddSwaggerUI = false` to serve OpenAPI documents without UI, `DrnPageOptions.FaviconUrl`, pagination query/body OpenAPI schemas, and representative 400 Problem Details metadata.
*   **HTMX error responses**: Added `DrnApiErrorOptions.HandleHtmxRequests` (default `false`) to return API Problem Details for failed HTMX requests.
*   **Scoped Problem Details and opt-out**: Added `ScopeContext.Problem` public defaults with `instance` defaulting to `PathBase + Path`, plus `[DisableDrnProblemDetails]` to opt controllers or actions out of DRN formatting and Swagger error metadata.
*   **Custom browser error pages**: Added `DrnPageOptions` for application-owned 5xx, 404, and 400 Razor pages (`ServerErrorPageModel`, `NotFoundPageModel`, `BadRequestPageModel`), plus `AccessDeniedPageModel` and `CookieAuthenticationEvents.PreserveAccessDeniedRequestId()`.
*   **Endpoint and application URLs**: Added `Url.Endpoint`, `LinkGenerator.GetUriByEndpoint`, `HttpRequest.ApplicationUrl`, and `ApplicationUrlTagHelper` for `~/` URLs preserving deployment prefixes.

### Changed

*   **Static `/robots.txt` caching**: Static `/robots.txt` responses now require cache revalidation by default.
*   **Exception and request diagnostics**: Buffered request-body capture continues across short reads up to the limit, and exception pages prioritize request protocol, scheme, method, and body before endpoint metadata. `Request_IpAddress` records an empty string when unavailable and empty `l5d-client-id` headers are omitted.
*   **Diagnostic redaction**: HTTP and startup logs redact explicitly classified data within capture limits without runtime name inference, and URL processing strips credentials and validates encoding.
*   **Swagger configuration and 64-bit integers**: Default Swagger UI document URLs resolve from the OpenAPI route with deployment prefixes, and `long`/`long?` schemas accept both JSON numbers and decimal strings.
*   **Rate limit defaults and diagnostics**: Increased defaults to 250 tokens/minute post-auth and 2,500 tokens/minute pre-auth, and added rule action fields to rejection logs.

### Bug Fixes

*   **Endpoint accessors**: `Path(Guid, template)` now honors the supplied placeholder. Missing endpoint bindings report discovery guidance, and multiple route selectors on one MVC action no longer fail catalog initialization.
*   **Swagger security**: Anonymous operations no longer advertise the default bearer authentication requirement.
*   **Developer exception pages**: Panel controls follow document focus order, support Enter and Space, announce the active panel to assistive technology, and include a favicon.
*   **Exception details serialization**: Prevented exception handling initialization failures after shared JSON options were already used for serialization.
*   **Started response exceptions**: `HttpScopeMiddleware` logs and rethrows the original exception when `context.Response.HasStarted` instead of failing on status assignment.
*   **Page rendering**: `IPageUtils.RenderPageAsync` now supports `_ViewStart`, layouts, and endpoint links for unmatched requests and awaits scoped disposal on success or failure.
*   **Developer JSON viewer**: Updated bundled JSONEditor to 10.4.3, fixed dark-theme search highlighting and default-CSP style blocks, and disabled Repair JSON and validation workers on the read-only viewer.
*   **Redirects**: Fixed errors converting started redirects to HTTP 303 and corrected the logged response status.
*   **Deployment prefixes**: Swagger URLs, developer error-page assets, active navigation, login and MFA redirects, and Vite assets now preserve deployment prefixes.

## Version 0.10.0

### Breaking Changes

*   **Generation Time at Startup**: Hosts validate generation time before constructors and hooks, including read-only and temporary hosts. The default minimum is `2026-09-09T00:00:00Z`. Configure `SourceKnownIdSettings` before startup or first ID/epoch use; an explicit `DefaultEpoch` requires `MinimumUtc`. Keep the origin consistent across the dataset's services and restarts. See [startup configuration](README.md#lifecycle--execution-flow).
*   **Swagger UI Prefix**: Enabled Swagger UI requires a `RoutePrefix` that is nonempty after trimming slashes. Replace root mounts with a prefix such as `swagger` or `docs/swagger`; its subtree is reserved for Swagger CSP.
*   **Razor Visibility**: `authorized-only` now checks authentication rather than completed MFA. Both `authorized-only` and `anonymous-only` are presence-only markers, including when set to `"false"`; their boolean TagHelper properties were removed. Use bare attributes or omit them to disable filtering. Use `policy-only="PolicyName"` with optional `policy-resource` for policy-specific visibility. Endpoint authorization remains required.
*   **MFA Policies and Exemptions**: Call `AddDrnIdentityMfaPolicies()` after Identity registration when using `IdentityManagementControllerBase`. After enrollment, factor management requires completed MFA and returns HTTP 403 when denied. Exempt schemes must also be selected for authentication; programmatic authorization without an HTTP policy context no longer uses scheme exemptions.
*   **Security Stamp Hook**: `ConfigureSecurityStampValidatorOptions` now receives `(SecurityStampValidatorOptions, IAppSettings, AuthenticationClaimConfig)` instead of returning an action. Update overrides and forward arguments to base. Configure claim mappings through `ConfigureAuthenticationClaims()`; changing canonical subject types may require reauthentication.
*   **MFA Setup Response**: With MFA globally enforced, password-valid accounts without two-factor authentication receive HTTP 200 with a five-minute setup credential. Cookie requests receive an empty response and a non-persistent, non-refreshable setup cookie; bearer requests receive `AccessTokenResponse` with `ExpiresIn = 300` and an empty `RefreshToken`. Use this credential with `TwoFactorAuth`, enable two-factor authentication, discard it, and log in again with an authenticator or recovery code.

### Security

*   **Swagger CSP**: Added replaceable `CspFor.CspPolicySwagger` with same-origin scripts and inline styles, preserving document caching. Selection respects path segment boundaries under the validated UI prefix. Disabled Swagger uses the default policy; policies outside its subtree retain nonce-based styles.
*   **Authorization Audit Events**: `HostingLogEvents` in `DRN.Framework.Hosting.Logging` exposes challenge, forbid, and exemption events (7401–7403) for filtering and alerts. Factor, recovery, and revocation events are not included.
*   **Forwarded Headers**: Settings accept CIDR networks and proxy addresses; invalid formats throw `ConfigurationException`. Defaults trust RFC 1918 private networks and loopback with `ForwardLimit = 2`. `TrustPrivateNetworks = false` removes private-network defaults. A nonempty `KnownIPNetworks` replaces network defaults; `KnownProxies` adds to existing trust.

### New Features

*   **Authentication and Renewal Hooks**: `ConfigureAuthenticationClaims()` centralizes subject, name, email, role, and MFA mappings. Identity applications can use defaults; external providers need their own integration. Added `ConfigureIdentityRenewal` and `DrnSignInManager<TUser>` for sign-in and refresh integration; register the manager explicitly.
*   **Application Builder Hook**: Added a `CreateApplicationAsync` overload accepting a builder callback for host integrations.

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
