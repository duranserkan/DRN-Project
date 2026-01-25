# DRN.Framework.Hosting

[![master](https://github.com/duranserkan/DRN-Project/actions/workflows/master.yml/badge.svg?branch=master)](https://github.com/duranserkan/DRN-Project/actions/workflows/master.yml)
[![develop](https://github.com/duranserkan/DRN-Project/actions/workflows/develop.yml/badge.svg?branch=develop)](https://github.com/duranserkan/DRN-Project/actions/workflows/develop.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)

[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=bugs)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=ncloc)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)

**DRN.Framework.Hosting** provides the **application shell** for DRN web applications. It abstracts away the boilerplate of configuring reliable, secure, and observable ASP.NET Core applications.

## Table of Contents

- [TL;DR](#tldr)
- [Directory Structure](#directory-structure)
- [QuickStart](#quickstart)
- [Lifecycle & Execution Flow](#lifecycle--execution-flow)
- [DrnProgramBase Deep Dive](#drnprogrambase-deep-dive)
- [Configuration](#configuration)
- [Security Features](#security-features)
- [Endpoint Management](#endpoint-management)
- [Razor TagHelpers](#razor-taghelpers)
- [Local Development](#local-development-infrastructure)
- [Global Usings](#global-usings)

## TL;DR
* **Secure by Default**: Enforces MFA (Fail-Closed), strict CSP with Nonces, and HSTS automatically.
* **Opinionated Startup**: `DrnProgramBase` creates a predictable lifecycle with 20+ overrideable hooks.
* **Type-Safe Routing**: Replaces "magic strings" with typed `Endpoint` and `Page` accessors.
* **Zero-Configuration Infrastructure**: Auto-provisions Postgres/RabbitMQ containers in `Debug` mode via `DRN.Framework.Testing`.
* **Frontend Synergy**: Includes TagHelpers for Vite manifest resolution, CSRF for HTMX, and secure asset loading.

## Directory Structure
```
DRN.Framework.Hosting/
├── DrnProgram/       # DrnProgramBase, options, actions, conventions
├── Endpoints/        # EndpointCollectionBase, PageForBase, type-safe accessors
├── Auth/             # Policies, MFA configuration, requirements
├── Consent/          # GDPR cookie consent management
├── Identity/         # Identity integration and scoped user middleware
├── Middlewares/      # HttpScopeLogger, exception handling, security middlewares
├── TagHelpers/       # Razor TagHelpers (Vite, Nonce, CSRF, Auth-Only)
├── Areas/            # Framework-provided Razor Pages (e.g., Error pages)
└── wwwroot/          # Framework style and script assets
```

## QuickStart

### 1. Basic Program
All DRN web apps inherit from `DrnProgramBase<TProgram>` to inherit the lifecycle hooks and default behaviors.

```csharp
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.HealthCheck;

namespace Sample.Hosted;

public class Program : DrnProgramBase<Program>, IDrnProgram
{
    // Entry Point (Runs the opinionated bootstrapping)
    public static async Task Main(string[] args) => await RunAsync(args);

    // [Required] Service Registration Hook
    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.AddSampleInfraServices(appSettings);
        builder.Services.AddSampleApplicationServices();
        return Task.CompletedTask;
    }
}

// Immediate API endpoint for testing (Inherits [AllowAnonymous] and Get())
[Route("[controller]")]
public class WeatherForecastController : WeatherForecastControllerBase;
```

### 2. Testing Integration
Test your application using `DRN.Framework.Testing` to spin up the full pipeline including databases.

```csharp
[Theory, DataInline]
public async Task WeatherForecast_Should_Return_Data(DrnTestContext context, ITestOutputHelper outputHelper)
{
    // Arrange
    var client = await context.ApplicationContext.CreateClientAsync<Program>(outputHelper);
    
    // Act
    var response = await client.GetAsync("WeatherForecast");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var data = await response.Content.ReadFromJsonAsync<IEnumerable<WeatherForecast>>();
    data.Should().NotBeEmpty();
}
```

## Lifecycle & Execution Flow

`DrnProgramBase` orchestrates the application startup to ensure security headers, logging scopes, and validation logic run in the correct order. Use `DrnProgramActions` to intercept these phases without cluttering your main Program class.

```mermaid
graph TD
    Start["RunAsync()"] --> CAB["CreateApplicationBuilder()"]
    
    subgraph "1. Builder Phase (Services & Config)"
    CAB --> CSO["ConfigureSwaggerOptions()"]
    CAB --> CDSH["ConfigureDefaultSecurityHeaders()"]
    CAB --> ASA["AddServicesAsync()"]
    ASA --> ABC["ApplicationBuilderCreatedAsync (Action)"]
    end

    ABC --> Build["builder.Build()"]
    
    subgraph "2. Application Phase (Middleware)"
    Build --> CA["ConfigureApplication()"]
    CA --> CAPS["ConfigureApplicationPipelineStart() (HSTS/Headers)"]
    CAPS --> LOG["HttpScopeMiddleware (TraceId/Logging)"]
    LOG --> UR["UseRouting()"]
    UR --> UA["UseAuthorization() (MFA Enforcement)"]
    UA --> CPSTA["ConfigureApplicationPostAuthorization() (Swagger UI)"]
    CPSTA --> MAE["MapApplicationEndpoints()"]
    end

    MAE --> ABA["ApplicationBuiltAsync (Action)"]
    ABA --> VE["ValidateEndpoints()"]
    VE --> VSA["ValidateServicesAsync()"]
    VSA --> AVA["ApplicationValidatedAsync (Action)"]
    AVA --> Run["application.RunAsync()"]
```

## DrnProgramBase Deep Dive

This section details the hooks available to customize your application's lifecycle. `DrnProgramBase` follows a **"Hook Method"** pattern: the base class defines the workflow, and you override virtual methods to inject logic.

### 1. Configuration Hooks (Builder Phase)

These hooks run while the `WebApplicationBuilder` is active, allowing you to configure the DI container and system options.

| Category | Method | Purpose |
| :--- | :--- | :--- |
| **OpenAPI** | `ConfigureSwaggerOptions` | Customize Swagger UI title, version, and visibility settings. |
| **MVC** | `ConfigureMvcBuilder` | Add `ApplicationParts`, custom formatters, or enable Razor Runtime Compilation. |
| **MVC** | `ConfigureMvcOptions` | Add global filters, conventions, or customize model binding. |
| **Auth** | `ConfigureAuthorizationOptions` | Define security policies. **Note**: Sets MFA as the default/fallback by default. |
| **Security** | `ConfigureDefaultSecurityHeaders` | Define global headers (HSTS, CSP, FrameOptions). |
| **Security** | `ConfigureDefaultCsp` | Customize CSP directives (Script, Image, Style sources). |
| **Cookies** | `ConfigureCookiePolicy` | Set GDPR consent logic and security attributes for all cookies. |
| **Infras.** | `ConfigureStaticFileOptions` | Customize caching (default: 1 year) and HTTPS compression. |
| **Infras.** | `ConfigureForwardedHeadersOptions` | Configure proxy/load-balancer header forwarding. |
| **Global** | `AddServicesAsync` | **[Required]** The primary place to register your application services. |

### 2. Pipeline Hooks (Application Phase)

These hooks define the request processing middleware sequence.

| Order | Hook | Typical Usage |
| :--- | :--- | :--- |
| **1** | `ConfigureApplicationPipelineStart` | `UseForwardedHeaders`, `UseHostFiltering`, `UseCookiePolicy`. |
| **2** | `ConfigureApplicationPreScopeStart` | `UseStaticFiles`. Runs before request logging/trace ID is established. |
| **3** | `ConfigureApplicationPostScopeStart` | Add middleware that needs access to `IScopedLog` but runs before routing. |
| **4** | `ConfigureApplicationPreAuthentication` | `UseRequestLocalization`. Runs before the user identity is resolved. |
| **5** | `ConfigureApplicationPostAuthentication` | `MfaRedirectionMiddleware`. Logic that runs after the user is known but before access checks. |
| **6** | `ConfigureApplicationPostAuthorization` | `UseSwaggerUI`. Runs after access is granted but before the final endpoint. |
| **7** | `MapApplicationEndpoints` | `MapControllers`, `MapRazorPages`, `MapHubs`. |

### 3. Verification Hooks

| Hook | Purpose |
| :--- | :--- |
| `ValidateEndpoints` | Ensures all type-safe endpoint accessors match actual mapped routes. |
| `ValidateServicesAsync` | Scans the container for `[Attribute]` based registrations and ensures they are resolvable. |

### 3. Internal Wiring (Automatic)

* **Service Validation**: Calls `ValidateServicesAsync` to scan `[Attribute]`-registered services and ensure they are resolvable at startup.
* **Secure JSON**: Enforces `HtmlSafeWebJsonDefaults` to prevent XSS via JSON serialization.
* **Endpoint Accessor**: Registers `IEndpointAccessor` for typed access to `EndpointCollectionBase`.

### 4. Properties

| Property | Default | Purpose |
|----------|---------|---------|
| `AppBuilderType` | `DrnDefaults` | Controls builder creation. Use `Slim` for minimal APIs. |
| `DrnProgramSwaggerOptions` | (Object) | Toggles Swagger generation. Defaults to `IsDevEnvironment`. |
| `NLogOptions` | (Object) | Controls NLog bootstrapping (e.g., replace logger factory). |

## Configuration

> [!TIP]
> **Configuration Precedence**: Environment > Secrets > AppSettings.
> Always use `User Secrets` for local connection strings to avoid committing credentials.

### Layering
1.  `appsettings.json`
2.  `appsettings.{Environment}.json`
3.  **User Secrets** (Development only)
4.  **Environment Variables** (`ASPNETCORE_`, `DOTNET_`)
5.  **Mounted Directories** (e.g. `/app/config`)
6.  **Command Line Arguments**

### Reference Configurations

#### NLog (Logging)
Standard configuration for Console and Graylog output.
```json
{
  "NLog": {
    "throwConfigExceptions": true,
    "targets": {
      "async": true,
      "console": {
        "type": "Console",
        "layout": "${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}"
      }
    },
    "rules": [
      { "logger": "*", "minLevel": "Info", "writeTo": "console" }
    ]
  }
}
```

#### Kestrel (Server)
```json
{
  "Kestrel": {
    "EndpointDefaults": { "Protocols": "Http1" },
    "Endpoints": {
      "All": { "Url": "http://*:5988" }
    }
  }
}
```

## Security Features

DRN Hosting enforces a **"Fail-Closed"** security model. If you forget to configure something, it remains locked.

### 1. MFA Enforcement (Fail-Closed)
The framework sets the `FallbackPolicy` for the entire application to require a Multi-Factor Authentication session. 
*   **Result**: Any new controller or page you add is **secure by default**. 
*   **Opt-Out**: Use `[AllowAnonymous]` or `[Authorize(Policy = AuthPolicy.MfaExempt)]` for single-factor pages like Login or MFA Setup.

### 2. Content Security Policy (Nonce-based)
DRN automatically generates a unique cryptographic nonce for every request.
*   **Automatic Protection**: Scripts and styles without a matching nonce are blocked by the browser, stopping most XSS attacks.
*   **Usage**: Use the `NonceTagHelper` (see below) to automatically inject these nonces.

### 3. Transparent Security Headers
Standard security headers are injected into every response:
*   **HSTS**: Strict-Transport-Security (2 years, includes subdomains).
*   **FrameOptions**: `DENY` (prevents clickjacking).
*   **ContentTypeOptions**: `nosniff`.
*   **ReferrerPolicy**: `strict-origin-when-cross-origin`.

### 4. GDPR & Cookie Security
Cookies are configured with `SameSite=Strict` and `HttpOnly` by default to mitigate CSRF and session hijacking. The `ConsentCookie` system ensures compliance with privacy regulations.


## Endpoint Management

Avoid "magic strings" in your code. DRN provides a type-safe way to reference routes that is verified at startup.

### 1. Define Your Accessors
Create a class inheriting from `EndpointCollectionBase<Program>` or `PageCollectionBase<Program>`.

```csharp
public class Get : EndpointCollectionBase<Program>
{
    public static UserEndpoints User { get; } = new();
}

public class UserEndpoints : ControllerForBase<UserController>
{
    // Template: /Api/User/[controller]/[action]
    public UserEndpoints() : base("/Api/User/[controller]") { }

    // Properties matching Controller Action names
    public ApiEndpoint Login { get; private set; } = null!;
    public ApiEndpoint Profile { get; private set; } = null!;
}
```

### 2. Usage in Code
Resolve routes at compile-time with full IDE support (intellisense).

```csharp
// Get the typed endpoint object
ApiEndpoint endpoint = Get.User.Login;

// Generate the path string
string url = endpoint.Path(); // "/Api/User/User/Login"

// Generate path with route parameters
string profileUrl = Get.User.ProfileDetail.Path(new() { ["id"] = userId.ToString() });
```

## Razor TagHelpers

| TagHelper | Target | Purpose |
| :--- | :--- | :--- |
| `ViteScriptTagHelper` | `<script src="buildwww/...">` | Resolves Vite manifest entries and adds subresource integrity. |
| `NonceTagHelper` | `<script>`, `<style>` | Automatically injects the request-specific CSP nonce. |
| `CsrfTokenTagHelper` | `hx-post`, `hx-put` | Automatically adds `RequestVerificationToken` to HTMX headers. |
| `AuthorizedOnlyTagHelper` | `*[authorized-only]` | Renders the element only if the user has an active MFA session. |
| `PageAnchorTagHelper` | `<a asp-page="...">` | Automatically adds `active` CSS class if the link matches current page. |

### Example: Secure Script Loading
```html
<!-- Input: Original Vite source path -->
<script src="buildwww/app/main.ts" crossorigin="anonymous"></script>

<!-- Output: Browser receives hashed path + integrity + nonce -->
<script src="/app/main.abc123.js" 
        integrity="sha256-xyz..." 
        nonce="random_nonce_here" 
        crossorigin="anonymous"></script>
```

## Local Development Infrastructure

Use `DRN.Framework.Testing` to provision infrastructure (Postgres, RabbitMQ) during local development without manual Docker management.

### 1. Add Conditional Reference
Add the following to your `.csproj` file to ensure the testing library (and its heavy dependencies like Testcontainers) is only included during development.

```xml
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
    <ProjectReference Include="..\DRN.Framework.Testing\DRN.Framework.Testing.csproj" />
</ItemGroup>
```

### 2. Configure Startup Actions
Implement `DrnProgramActions` to trigger the auto-provisioning.

```csharp
#if DEBUG
public class SampleProgramActions : DrnProgramActions
{
    public override async Task ApplicationBuilderCreatedAsync<TProgram>(
        TProgram program, WebApplicationBuilder builder,
        IAppSettings appSettings, IScopedLog scopedLog)
    {
        var options = new ExternalDependencyLaunchOptions
        {
            PostgresContainerSettings = new() 
            { 
                Reuse = true, // Faster restarts
                HostPort = 6432 // Avoid conflicts with local Postgres
            }
        };

        // Auto-starts containers if not running and updates AppSettings
        await builder.LaunchExternalDependenciesAsync(scopedLog, appSettings, options);
    }
}
#endif
```

## Global Usings

Standard global usings for Hosted applications to reduce boilerplate:
```csharp
global using DRN.Framework.Hosting.DrnProgram;
global using DRN.Framework.Hosting.Endpoints;
global using DRN.Framework.Utils.DependencyInjection;
global using DRN.Framework.Utils.Logging;
global using DRN.Framework.Utils.Settings;
global using Microsoft.AspNetCore.Mvc;
```

---
**Semper Progressivus: Always Progressive**