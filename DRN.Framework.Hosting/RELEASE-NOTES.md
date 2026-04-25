Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

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
        *   `ConfigureMvcBuilder` / `ConfigureMvcOptions`: Customize MVC conventions and runtime compilation.
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
    *   **Local Provisioning**: `LaunchExternalDependenciesAsync` auto-starts Postgres/RabbitMQ Testcontainers in Debug mode.
    *   **Validation**: `ValidateEndpoints` and `ValidateServicesAddedByAttributesAsync` ensure system integrity at startup.
    *   **Identity Integration**: `IdentityControllerBase` and `ScopedUserMiddleware` for deep identity context propagation.

---

Documented with the assistance of [DiSCOS](https://github.com/duranserkan/DRN-Project/blob/develop/DiSCOS/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
