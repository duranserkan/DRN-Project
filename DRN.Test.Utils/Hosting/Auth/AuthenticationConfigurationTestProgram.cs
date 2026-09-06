using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DRN.Test.Utils.Hosting.Auth;

// No Identity, external services, exemption/redirection overrides, or renewal bypass.
public sealed class AuthenticationConfigurationTestProgram : DrnProgramBase<AuthenticationConfigurationTestProgram>, IDrnProgram
{
    public const string ModeKey = "AuthenticationRegression:Mode";
    public const string FirstScheme = "RegressionFirst";
    public const string SecondScheme = "RegressionSecond";
    public const string CredentialHeader = "X-Regression-Credential";
    public const string Root = "/authentication-regression";

    public static Task Main(string[] args) => RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        var mode = appSettings.Configuration[ModeKey] ?? "implicit";
        if (mode != "implicit")
        {
            var authentication = builder.Services.AddAuthentication(options =>
            {
                if (mode == "default") options.DefaultScheme = FirstScheme;
                if (mode == "authenticate-only") options.DefaultAuthenticateScheme = FirstScheme;
            });
            if (mode != "services-only")
                authentication.AddScheme<AuthenticationSchemeOptions, ConfigurationAuthenticationHandler>(FirstScheme, _ => { });
            if (mode is "multiple" or "default" or "authenticate-only")
                authentication.AddScheme<AuthenticationSchemeOptions, ConfigurationAuthenticationHandler>(SecondScheme, _ => { });
        }

        builder.Services.AddAuthorizationBuilder().AddPolicy("RegressionSelected", policy =>
            policy.AddAuthenticationSchemes(SecondScheme).RequireAuthenticatedUser());
        builder.Services.AddSingleton<ConfigurationExceptionRecorder>();
        builder.Services.AddSingleton<IDrnExceptionFilter>(sp => sp.GetRequiredService<ConfigurationExceptionRecorder>());
        return Task.CompletedTask;
    }

    protected override void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        base.ConfigureSwaggerOptions(options, appSettings);
        options.AddSwagger = false;
    }

    protected override void ConfigureDefaultSecurityHeaders(HeaderPolicyCollection policies, IServiceProvider services, IAppSettings appSettings)
    {
        base.ConfigureDefaultSecurityHeaders(policies, services, appSettings);
        policies.AddCustomHeader("X-Program-Customization", "preserved");
    }

    protected override void ConfigureApplicationPreAuthentication(WebApplication application, IAppSettings appSettings)
    {
        base.ConfigureApplicationPreAuthentication(application, appSettings);
        application.Use(async (context, next) =>
        {
            context.Response.Headers["X-Before-Authentication"] = context.User.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous";
            await next(context);
        });
    }

    protected override void ConfigureApplicationPostAuthentication(WebApplication application, IAppSettings appSettings)
    {
        base.ConfigureApplicationPostAuthentication(application, appSettings);
        application.Use(async (context, next) =>
        {
            var user = context.RequestServices.GetRequiredService<IScopedUser>();
            context.Response.Headers["X-After-Authentication"] = user.Authenticated ? "authenticated" : "anonymous";
            await next(context);
        });
    }

    protected override void MapApplicationEndpoints(WebApplication application, IAppSettings appSettings)
    {
        base.MapApplicationEndpoints(application, appSettings);
        application.MapGet(Root + "/anonymous", () => "anonymous").AllowAnonymous();
        application.MapGet(Root + "/self", (HttpContext context) =>
        {
            context.Items[CspFor.CspPolicyName] = CspFor.CspPolicySelf;
            return "self";
        }).AllowAnonymous();
        application.MapGet(Root + "/inline", (HttpContext context) =>
        {
            context.Items[CspFor.CspPolicyName] = CspFor.CspPolicyInline;
            return "inline";
        }).AllowAnonymous();
        application.MapGet(Root + "/protected", PrincipalResult).RequireAuthorization();
        application.MapGet(Root + "/fallback", PrincipalResult);
        application.MapGet(Root + "/selected", PrincipalResult).RequireAuthorization("RegressionSelected");
    }

    private static IResult PrincipalResult(HttpContext context, IScopedUser user)
    {
        context.Response.Headers["X-Protected-Endpoint"] = "executed";
        return Results.Text($"{context.User.Identity?.AuthenticationType}|{user.Id}|{user.Authenticated}");
    }
}

internal sealed class ConfigurationAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var credential = Request.Headers[AuthenticationConfigurationTestProgram.CredentialHeader].ToString();
        if (string.IsNullOrEmpty(credential)) return Task.FromResult(AuthenticateResult.NoResult());
        if (credential is not ("plain" or "mfa")) return Task.FromResult(AuthenticateResult.Fail("Invalid test credential."));

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "regression-user")], Scheme.Name);
        if (credential == "mfa") identity.AddClaim(new Claim("amr", "mfa"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers["X-Challenge-Scheme"] = Scheme.Name;
        return base.HandleChallengeAsync(properties);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.Headers["X-Forbid-Scheme"] = Scheme.Name;
        return base.HandleForbiddenAsync(properties);
    }
}

// Observe the existing exception path without replacing rendering or returning diagnostic text to clients.
public sealed class ConfigurationExceptionRecorder : IDrnExceptionFilter
{
    public ConcurrentQueue<Exception> Exceptions { get; } = new();

    public Task<DrnExceptionFilterResult> HandlePreExceptionModelCreationAsync(HttpContext httpContext, Exception exception)
    {
        Exceptions.Enqueue(exception);
        return Task.FromResult(DrnExceptionFilterResult.Default);
    }

    public Task<DrnExceptionFilterResult> HandleExceptionAsync(HttpContext httpContext, Exception exception, DrnExceptionModel model) =>
        Task.FromResult(DrnExceptionFilterResult.Default);
}
