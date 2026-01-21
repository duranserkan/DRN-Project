## DRN Hosted App Expert

> Patterns and conventions for building web applications on DRN.Framework, based on Sample.Hosted reference implementation.

---

## Application Structure

```
[App].Hosted/
├── SampleProgram.cs           # Entry point, extends DrnProgramBase
├── SampleModule.cs            # Service composition root
├── SampleProgramActions.cs    # Lifecycle hooks (DEBUG only)
├── Controllers/               # API controllers by domain
│   └── [Domain]/              # e.g., Sample/, User/, QA/
├── Pages/                     # Razor Pages
│   ├── Shared/                # Layouts, partials, components
│   │   ├── _Layout.cshtml     # Main layout
│   │   ├── _Navbar.cshtml     # Navigation
│   │   ├── _Sidebar.cshtml    # Sidebar
│   │   └── Models/            # View models
│   └── [Feature]/             # e.g., User/, Test/, System/
├── Helpers/                   # Type-safe routing helpers
│   ├── _Get.cs                # Central access point
│   ├── PageFor/               # Razor page route definitions
│   └── EndpointFor/           # API endpoint definitions
├── Settings/                  # App-specific settings
├── TagHelpers/                # Custom tag helpers
├── Filters/                   # Action filters
├── Extensions/                # Extension methods
├── buildwww/                  # Frontend source (Vite)
├── wwwroot/                   # Static assets (build output)
├── appsettings.json           # Configuration
└── vite.config.js             # Frontend build config
```

---

## Program Entry Point

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

    // MFA redirection configuration
    protected override MfaRedirectionConfig ConfigureMFARedirection()
        => new(Get.Page.User.Management.EnableAuthenticator, 
               Get.Page.User.LoginWith2Fa,
               Get.Page.User.Login, 
               Get.Page.User.Logout, 
               Get.Page.All);

    // MFA exemption for API bearer tokens
    protected override MfaExemptionConfig ConfigureMFAExemption()
        => new() { ExemptAuthSchemes = [IdentityConstants.BearerScheme] };
}
```

---

## Module Composition

```csharp
public static class SampleModule
{
    public static IServiceCollection AddSampleHostedServices(
        this IServiceCollection services, 
        IAppSettings settings)
    {
        // Layer modules (dependency order)
        services.AddSampleInfraServices();      // Infrastructure first
        services.AddSampleApplicationServices(); // Application second
        
        // Identity configuration
        services.AddIdentityApiEndpoints<SampleUser>(ConfigureIdentity);
        
        // Data protection
        services.AddDrnDataProtectionContext()
                .AddDataProtection()
                .PersistKeysToDbContext<DrnDataProtectionContext>();
        
        // Attribute-based service registration
        services.AddServicesWithAttributes();
        
        return services;
    }
}
```

---

## Type-Safe Routing System

### Central Access Point
```csharp
public static class Get
{
    public static SamplePageFor Page { get; } = PageCollectionBase<SamplePageFor>.PageCollection;
    public static SampleEndpointFor Endpoint { get; } = 
        (SampleEndpointFor)EndpointCollectionBase<SampleProgram>.EndpointCollection!;
    
    public static CspFor Csp { get; } = new();
    public static RoleFor Role { get; } = new();
    public static ClaimFor Claim { get; } = new();
    public static TempDataKeys TempDataKeys { get; } = new();
    public static ViewDataKeys ViewDataKeys { get; } = new();
    public static SubNavigationFor SubNavigation { get; } = new();
}
```

### Page Routes (Razor Pages)
```csharp
public class SamplePageFor : PageCollectionBase<SamplePageFor>
{
    public RootPageFor Root { get; } = new();
    public UserPageFor User { get; } = new();
    public TestPageFor Test { get; } = new();
}

public class UserPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["User"];
    
    public string Login { get; init; } = string.Empty;
    public string Logout { get; init; } = string.Empty;
    public string Register { get; init; } = string.Empty;
    public UserProfilePageFor Profile { get; } = new();
}
```

### API Endpoints
```csharp
public class SampleEndpointFor : EndpointCollectionBase<SampleProgram>
{
    public UserApiFor User { get; } = new();
    public SampleApiFor Sample { get; } = new();
}

public class SampleApiFor
{
    public const string Prefix = "/Api/Sample";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";
    
    public WeatherForecastFor WeatherForecast { get; } = new();
}

public class WeatherForecastFor() 
    : ControllerForBase<WeatherForecastController>(SampleApiFor.ControllerRouteTemplate)
{
    // Property name must match action name, setter required
    public ApiEndpoint Get { get; private set; } = null!;
}
```

### Usage in Code
```csharp
// In Razor pages
<a asp-page="@Get.Page.User.Login">Login</a>
return RedirectToPage(Get.Page.User.Profile.Details);

// In controllers
[Route(SampleApiFor.ControllerRouteTemplate)]
public class WeatherForecastController : ControllerBase { }

// In MFA configuration
ConfigureMFARedirection() => new(Get.Page.User.Management.EnableAuthenticator, ...);
```

---

## Controller Pattern

```csharp
[ApiController]
[Route(SampleApiFor.ControllerRouteTemplate)]
public class PrivateController(IScopedUser scopedUser, IScopedLog scopedLog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public ActionResult<IScopedUser> Authorized() => Ok(scopedUser);

    [AllowAnonymous]
    [HttpGet("anonymous")]
    public ActionResult<IScopedUser> Anonymous() => Ok(scopedUser);

    [HttpGet("scope-summary")]
    public ActionResult<ScopeSummary> Context() => Ok(ScopeContext.Value.GetScopeSummary());
}
```

---

## Razor Page Pattern

```csharp
[AllowAnonymous]
public class LoginModel(SignInManager<SampleUser> signInManager, UserManager<SampleUser> userManager) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = null!;

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (ScopeContext.Authenticated)
            return RedirectToPage(Get.Page.Root.Home);
        
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : string.Empty;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        
        // ... authentication logic
        return RedirectToPage(Get.Page.User.LoginWith2Fa, 
            new { Input.ReturnUrl, Input.RememberMe });
    }
}

public class LoginInput
{
    [Required, EmailAddress]
    public string Email { get; init; } = null!;
    
    [Required, DataType(DataType.Password)]
    public string Password { get; init; } = null!;
    
    [Display(Name = "Remember me?")]
    public bool RememberMe { get; init; }
}
```

---

## Layout System

### View Imports (_ViewImports.cshtml)
```razor
@using DRN.Framework.Hosting.TagHelpers
@using DRN.Framework.Utils.Scope
@using Sample.Hosted.Helpers
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, DRN.Framework.Hosting
@addTagHelper *, Sample.Hosted
```

### Layout with ScopeContext
```razor
@{
    Layout = "_LayoutBase";
    var isHtmxRequest = Context.Request.Headers.ContainsKey("HX-Request");
    if (isHtmxRequest) Layout = "_LayoutBaseHtmx";
}

@if (ScopeContext.MFACompleted)
{
    <partial name="_Sidebar"/>
}

<main>
    @RenderBody()
</main>
```

---

## Identity Configuration

```csharp
// Settings/IdentitySettings.cs
public static class IdentitySettings
{
    public static readonly PasswordOptions PasswordOptions = new()
    {
        RequireDigit = true,
        RequireUppercase = true,
        RequireLowercase = true,
        RequiredLength = 8,
        RequireNonAlphanumeric = true
    };

    public static readonly LockoutOptions LockoutOptions = new()
    {
        MaxFailedAccessAttempts = 3,
        DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1)
    };
}

// Domain/Users/SampleUser.cs
public class SampleUser : IdentityUser { }

// Infra/Identity/SampleIdentityContext.cs
public class SampleIdentityContext : DrnContextIdentity<SampleIdentityContext, SampleUser>
{
    public DbSet<ProfilePicture> ProfilePictures { get; set; }
}
```

---

## Frontend Build (Vite)

### Configuration
```javascript
// vite.config.js
const builds = {
    app: {
        build: {
            outDir: 'wwwroot/app',
            manifest: true,
            rollupOptions: {
                input: {
                    app_preload: resolve(__dirname, 'buildwww/app/js/appPreload.js'),
                    app_postload: resolve(__dirname, 'buildwww/app/js/appPostload.js')
                }
            }
        }
    },
    bootstrap: {
        build: {
            outDir: 'wwwroot/lib/bootstrap',
            rollupOptions: {
                input: {
                    bootstrap: resolve(__dirname, 'buildwww/lib/bootstrap/bootstrap.scss'),
                    bootstrap_bundle: resolve(__dirname, 'buildwww/lib/bootstrap/bootstrap.js')
                }
            }
        }
    }
};
```

### Build Commands
```bash
npm run build                    # Default (app)
BUILD_TYPE=bootstrap npm run build # Bootstrap bundle
BUILD_TYPE=htmx npm run build    # HTMX bundle
```

---

## Domain Entity Pattern

```csharp
// Domain/SampleEntityTypes.cs
public enum SampleEntityTypes : byte
{
    Answer = 1,
    Category = 3,
    Question = 4,
    User = 7
}

// Domain/Users/User.cs
[EntityType((int)SampleEntityTypes.User)]
public class User : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string UserName { get; private set; } = null!;
    public ContactDetail Contact { get; private set; } = null!;  // [Owned]
    public Address Address { get; private set; } = null!;        // [Owned]
}
```

---

## Development Lifecycle Hooks

```csharp
#if DEBUG
public class SampleProgramActions : DrnProgramActions
{
    public override async Task ApplicationBuilderCreatedAsync<TProgram>(
        TProgram program, 
        WebApplicationBuilder builder,
        IAppSettings appSettings, 
        IScopedLog scopedLog)
    {
        // Launch external dependencies (Docker containers)
        await builder.LaunchExternalDependenciesAsync(scopedLog, appSettings, 
            new ExternalDependencyLaunchOptions
            {
                PostgresContainerSettings = new PostgresContainerSettings
                {
                    Reuse = true,
                    HostPort = 6432
                }
            });
    }
}
#endif
```

---

## AppSettings Structure

```json
{
  "Environment": "Development",
  "ApplicationName": "DRN Sample",
  "AllowedHosts": "*",
  "HostOptions": {
    "ShutdownTimeout": "00:00:15",
    "BackgroundServiceExceptionBehavior": "StopHost"
  },
  "Kestrel": {
    "Endpoints": {
      "All": { "Url": "http://*:5998" }
    }
  },
  "DrnLocalizationSettings": {
    "Enabled": true,
    "SupportedCultures": ["tr", "en", "de-CH"],
    "DefaultCulture": "en"
  },
  "NLog": { /* structured logging config */ }
}
```

---

## Key Conventions Summary

| Convention | Pattern |
|------------|---------|
| Program inheritance | `class Program : DrnProgramBase<Program>, IDrnProgram` |
| Module composition | `services.AddSampleInfraServices().AddSampleApplicationServices()` |
| Service registration | `services.AddServicesWithAttributes()` |
| Page routes | `Get.Page.[Area].[Page]` |
| API endpoints | `Get.Endpoint.[Domain].[Controller].[Action]` |
| Entity types | `enum EntityTypes : byte` + `[EntityType(n)]` |
| Identity context | `class Context : DrnContextIdentity<Context, TUser>` |
| MFA configuration | Override `ConfigureMFARedirection()` in Program |
| Frontend build | Vite with manifest → `wwwroot/` |
| Debug containers | `DrnProgramActions.ApplicationBuilderCreatedAsync()` |

---
