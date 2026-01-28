Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.7.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals.

> [!WARNING]
> Since v0.6.0 (released 10 November 2024), substantial changes have occurred. This release notes file has been reset to reflect the current state of the project as of 29 January 2026. Previous history has been archived to maintain a clean source of truth based on the current codebase.

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
        *   `ConfigureStaticFileOptions` / `ConfigureResponseCachingOptions`: Optimize asset delivery.
    *   **Pipeline Phase**:
        *   `ConfigureApplicationPipelineStart`: HSTS, Forwarded Headers.
        *   `ConfigureApplicationPreScopeStart`: Static files (pre-auth).
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
*   **Infrastructure & Development**
    *   **Local Provisioning**: `LaunchExternalDependenciesAsync` auto-starts Postgres/RabbitMQ Testcontainers in Debug mode.
    *   **Validation**: `ValidateEndpoints` and `ValidateServicesAddedByAttributesAsync` ensure system integrity at startup.
    *   **Identity Integration**: `IdentityControllerBase` and `ScopedUserMiddleware` for deep identity context propagation.

---

**Semper Progressivus: Always Progressive**