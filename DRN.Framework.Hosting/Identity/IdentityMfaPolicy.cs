using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.Hosting.Identity;

/// <summary>Opt-in policies for local ASP.NET Core Identity enrollment and challenge endpoints.</summary>
public static class IdentityMfaPolicy
{
    // TODO(MFA-05): Add passkey registration/authentication and recovery integration; grant
    // assurance only after verifying user verification, and prevent weaker fallback from bypassing policy.
    public const string Enrollment = "IdentityMfaEnrollment";
    public const string BrowserEnrollment = "IdentityBrowserMfaEnrollment";
    public const string Challenge = "IdentityMfaChallenge";

    /// <param name="services">The services containing ASP.NET Core Identity authentication.</param>
    /// <param name="identityApiScheme">The cookie/bearer composite registered by AddIdentityApiEndpoints, or an application-owned equivalent.</param>
    public static IServiceCollection AddDrnIdentityMfaPolicies(this IServiceCollection services, string identityApiScheme = "Identity.BearerAndApplication")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityApiScheme);
        services.TryAddSingleton(AuthenticationClaimConfig.Default);
        services.AddAuthorization();
        services.AddOptions<AuthorizationOptions>().Configure<AuthenticationClaimConfig>((options, claims) =>
        {
            options.AddPolicy(Enrollment, policy => ConfigureEnrollment(policy, identityApiScheme, claims));
            options.AddPolicy(BrowserEnrollment, policy => ConfigureEnrollment(policy, IdentityConstants.ApplicationScheme, claims));
            options.AddPolicy(Challenge, policy => policy
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .AddRequirements(new MfaExemptRequirement())
                .RequireAssertion(context => MfaPrincipal.HasSingleAccount(context.User, claims, requireSubject: true) &&
                    MfaPrincipal.HasState(context.User, MfaClaimValues.MfaInProgress) &&
                    !MfaPrincipal.HasState(context.User, MfaClaimValues.MfaSetupRequired)));
        });
        return services;
    }

    private static void ConfigureEnrollment(AuthorizationPolicyBuilder policy, string scheme, AuthenticationClaimConfig claims) => policy
        .AddAuthenticationSchemes(scheme)
        .AddRequirements(new MfaExemptRequirement())
        .RequireAssertion(context => MfaPrincipal.HasSingleAccount(context.User, claims, requireSubject: true) &&
            !MfaPrincipal.HasState(context.User, MfaClaimValues.MfaInProgress));

    /// <summary>Must be checked against the final authorized user before reading or modifying factor data.</summary>
    public static bool CanManage(ClaimsPrincipal user, bool factorEnabled, bool mfaEnforced, AuthenticationClaimConfig config)
    {
        if (!MfaPrincipal.HasSingleAccount(user, config, requireSubject: true) || MfaPrincipal.HasState(user, MfaClaimValues.MfaInProgress))
            return false;
        if (MfaPrincipal.IsCompleted(user, config))
            return true;
        return !factorEnabled && (!mfaEnforced || MfaPrincipal.HasState(user, MfaClaimValues.MfaSetupRequired));
    }
}
