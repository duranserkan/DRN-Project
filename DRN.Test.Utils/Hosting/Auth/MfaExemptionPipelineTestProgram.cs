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
using Microsoft.Extensions.Options;

namespace DRN.Test.Utils.Hosting.Auth;

public sealed class MfaExemptionPipelineTestProgram : DrnProgramBase<MfaExemptionPipelineTestProgram>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = MfaPipelineTestValues.AuthenticationScheme;
                options.DefaultChallengeScheme = MfaPipelineTestValues.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, MfaPipelineTestAuthHandler>(
                MfaPipelineTestValues.AuthenticationScheme,
                _ => { })
            .AddScheme<AuthenticationSchemeOptions, MfaPipelineTestAuthHandler>(
                MfaPipelineTestValues.NamedAuthenticationScheme,
                _ => { });

        builder.Services.AddAuthorization(options =>
            options.AddPolicy(MfaPipelineTestValues.NamedSchemePolicy, policy =>
            {
                policy.AuthenticationSchemes.Add(MfaPipelineTestValues.NamedAuthenticationScheme);
                policy.RequireClaim(MfaPipelineTestValues.NamedSchemeClaim, MfaPipelineTestValues.NamedSchemeClaimValue);
            }));
        builder.Services.AddServicesWithAttributes();
        return Task.CompletedTask;
    }

    protected override void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        base.ConfigureSwaggerOptions(options, appSettings);
        options.AddSwagger = false;
    }

    protected override MfaExemptionConfig ConfigureMFAExemption() =>
        new() { ExemptAuthSchemes = [MfaPipelineTestValues.AuthenticationScheme] };

    protected override MfaClaimConfig ConfigureMFAClaim() =>
        new(MfaPipelineTestValues.MfaClaimType, MfaPipelineTestValues.MfaClaimValue);

    protected override void MapApplicationEndpoints(WebApplication application, IAppSettings appSettings)
    {
        base.MapApplicationEndpoints(application, appSettings);
        application.MapGet(MfaPipelineTestValues.ProtectedPath, () => Results.Ok())
            .RequireAuthorization();
        application.MapGet(MfaPipelineTestValues.RoleProtectedPath, () => Results.Ok())
            .RequireAuthorization(policy => policy.RequireRole(MfaPipelineTestValues.RequiredRole));
        application.MapGet(MfaPipelineTestValues.NamedSchemeProtectedPath,
                (HttpContext context) => Results.Text(context.User.Identity?.AuthenticationType ?? string.Empty))
            .RequireAuthorization(MfaPipelineTestValues.NamedSchemePolicy);
    }
}

internal sealed class MfaPipelineTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(MfaPipelineTestValues.CredentialHeader, out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        var credential = values.ToString();
        if (credential == MfaPipelineTestValues.InvalidCredential)
            return Task.FromResult(AuthenticateResult.Fail("The test credential is invalid."));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "mfa-pipeline-user"),
            new(ClaimTypes.Name, "mfa-pipeline-user"),
            new(ClaimTypes.Role, MfaPipelineTestValues.RequiredRole)
        };

        if (Scheme.Name == MfaPipelineTestValues.NamedAuthenticationScheme)
            claims.Add(new Claim(MfaPipelineTestValues.NamedSchemeClaim, MfaPipelineTestValues.NamedSchemeClaimValue));

        switch (credential)
        {
            case MfaPipelineTestValues.CompletedCredential:
                claims.Add(new Claim(MfaPipelineTestValues.MfaClaimType, MfaPipelineTestValues.MfaClaimValue));
                break;
            case MfaPipelineTestValues.PasswordCredential:
                claims.Add(new Claim(ClaimConventions.AuthenticationMethodReference, "pwd"));
                break;
            case MfaPipelineTestValues.SetupCredential:
                claims.Add(new Claim(ClaimConventions.AuthenticationMethod, MfaClaimValues.MfaSetupRequired));
                break;
            case MfaPipelineTestValues.SetupAndCompletedCredential:
                claims.Add(new Claim(ClaimConventions.AuthenticationMethod, MfaClaimValues.MfaSetupRequired));
                claims.Add(new Claim(MfaPipelineTestValues.MfaClaimType, MfaPipelineTestValues.MfaClaimValue));
                break;
            default:
                return Task.FromResult(AuthenticateResult.Fail("The test credential is unknown."));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public static class MfaPipelineTestValues
{
    public const string AuthenticationScheme = "MfaPipelineTest";
    public const string NamedAuthenticationScheme = "MfaPipelineNamedTest";
    public const string NamedSchemePolicy = "MfaPipelineNamedScheme";
    public const string NamedSchemeClaim = "mfa-pipeline-auth-source";
    public const string NamedSchemeClaimValue = "named";
    public const string MfaClaimType = "acr";
    public const string MfaClaimValue = "urn:drn:test:mfa";
    public const string ProtectedPath = "/mfa/protected";
    public const string RoleProtectedPath = "/mfa/role-protected";
    public const string NamedSchemeProtectedPath = "/mfa/named-scheme-protected";
    public const string RequiredRole = "mfa-pipeline-role";
    public const string CredentialHeader = "X-Test-Mfa-Credential";
    public const string InvalidCredential = "invalid";
    public const string PasswordCredential = "password";
    public const string SetupCredential = "setup";
    public const string SetupAndCompletedCredential = "setup-and-completed";
    public const string CompletedCredential = "completed";
}
