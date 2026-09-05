using System.Security.Claims;
using System.Text.Encodings.Web;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DRN.Test.Utils.Hosting.Auth;

public sealed class NonDefaultExemptSchemeTestProgram : DrnProgramBase<NonDefaultExemptSchemeTestProgram>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = NonDefaultExemptValues.DefaultCookieScheme;
                options.DefaultChallengeScheme = NonDefaultExemptValues.DefaultCookieScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, NonDefaultCookieAuthHandler>(
                NonDefaultExemptValues.DefaultCookieScheme,
                _ => { })
            .AddScheme<AuthenticationSchemeOptions, NonDefaultApiKeyAuthHandler>(
                NonDefaultExemptValues.NonDefaultApiKeyScheme,
                _ => { })
            .AddScheme<AuthenticationSchemeOptions, NonDefaultApiKeyAuthHandler>(
                NonDefaultExemptValues.SecondApiKeyScheme,
                _ => { });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(NonDefaultExemptValues.ApiKeyPolicy, policy =>
            {
                policy.AuthenticationSchemes.Add(NonDefaultExemptValues.NonDefaultApiKeyScheme);
                policy.RequireRole(NonDefaultExemptValues.ManagerRole);
            });
            options.AddPolicy(NonDefaultExemptValues.SecondApiKeyScheme, policy =>
                policy.AddAuthenticationSchemes(NonDefaultExemptValues.SecondApiKeyScheme)
                    .RequireRole(NonDefaultExemptValues.ManagerRole));
        });

        builder.Services.AddServicesWithAttributes();
        return Task.CompletedTask;
    }

    protected override void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        base.ConfigureSwaggerOptions(options, appSettings);
        options.AddSwagger = false;
    }

    protected override MfaExemptionConfig ConfigureMFAExemption() =>
        new() { ExemptAuthSchemes = [NonDefaultExemptValues.NonDefaultApiKeyScheme, NonDefaultExemptValues.SecondApiKeyScheme] };

    protected override void MapApplicationEndpoints(WebApplication application, IAppSettings appSettings)
    {
        base.MapApplicationEndpoints(application, appSettings);

        // Role endpoint relying on default authentication scheme (DefaultCookie)
        application.MapGet(NonDefaultExemptValues.CookieRoleProtectedPath, () => Results.Ok())
            .RequireAuthorization(policy => policy.RequireRole(NonDefaultExemptValues.ManagerRole));

        // Endpoint explicitly opting into the non-default ApiKey scheme
        application.MapGet(NonDefaultExemptValues.ApiKeyProtectedPath, () => Results.Ok())
            .RequireAuthorization(NonDefaultExemptValues.ApiKeyPolicy);
        application.MapGet(NonDefaultExemptValues.SecondApiKeyProtectedPath, (HttpContext context) => Results.Text(context.User.Identity!.AuthenticationType!))
            .RequireAuthorization(NonDefaultExemptValues.SecondApiKeyScheme);
    }
}

internal sealed class NonDefaultCookieAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(NonDefaultExemptValues.CookieHeader, out var values) ||
            string.IsNullOrWhiteSpace(values.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "cookie-user"),
            new Claim(ClaimTypes.Role, NonDefaultExemptValues.ManagerRole),
            new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr)
        ], Scheme.Name);

        if (values.ToString() == "password")
            identity.RemoveClaim(identity.FindFirst(ClaimConventions.AuthenticationMethodReference)!);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

internal sealed class NonDefaultApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Scheme.Name == NonDefaultExemptValues.SecondApiKeyScheme
            ? NonDefaultExemptValues.SecondApiKeyHeader : NonDefaultExemptValues.ApiKeyHeader;
        if (!Request.Headers.TryGetValue(header, out var values) ||
            string.IsNullOrWhiteSpace(values.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "api-key-user"),
            new Claim(ClaimTypes.Role, NonDefaultExemptValues.ManagerRole)
        ], Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

public static class NonDefaultExemptValues
{
    public const string DefaultCookieScheme = "DefaultCookie";
    public const string NonDefaultApiKeyScheme = "CustomApiKey";
    public const string ApiKeyPolicy = "ApiKeyPolicy";
    public const string SecondApiKeyScheme = "SecondApiKey";
    public const string SecondApiKeyHeader = "X-Second-Api-Key";
    public const string SecondApiKeyProtectedPath = "/mfa/second-key-protected";
    public const string ManagerRole = "Manager";
    public const string CookieHeader = "X-Cookie-Auth";
    public const string ApiKeyHeader = "X-Api-Key-Auth";
    public const string CookieRoleProtectedPath = "/mfa/cookie-role-protected";
    public const string ApiKeyProtectedPath = "/mfa/apikey-protected";
}
