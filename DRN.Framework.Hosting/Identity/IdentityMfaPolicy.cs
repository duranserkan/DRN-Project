using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Identity;

/// <summary>Opt-in policies for local ASP.NET Core Identity enrollment and challenge endpoints.</summary>
public static class IdentityMfaPolicy
{
    public const string Enrollment = "IdentityMfaEnrollment";
    public const string BrowserEnrollment = "IdentityBrowserMfaEnrollment";
    public const string Challenge = "IdentityMfaChallenge";

    /// <param name="services">The services containing ASP.NET Core Identity authentication.</param>
    /// <param name="identityApiScheme">The cookie/bearer composite registered by AddIdentityApiEndpoints, or an application-owned equivalent.</param>
    public static IServiceCollection AddDrnIdentityMfaPolicies(this IServiceCollection services,
        string identityApiScheme = "Identity.BearerAndApplication")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityApiScheme);
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Enrollment, policy => ConfigureEnrollment(policy, identityApiScheme));
            options.AddPolicy(BrowserEnrollment, policy => ConfigureEnrollment(policy, IdentityConstants.ApplicationScheme));
            options.AddPolicy(Challenge, policy => policy
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .AddRequirements(new MfaExemptRequirement())
                .RequireAssertion(context => MfaPrincipal.HasSingleAccount(context.User) &&
                    MfaPrincipal.HasState(context.User, MfaClaimValues.MfaInProgress) &&
                    !MfaPrincipal.HasState(context.User, MfaClaimValues.MfaSetupRequired)));
        });
        return services;
    }

    private static void ConfigureEnrollment(AuthorizationPolicyBuilder policy, string scheme) => policy
        .AddAuthenticationSchemes(scheme)
        .AddRequirements(new MfaExemptRequirement())
        .RequireAssertion(context => MfaPrincipal.HasSingleAccount(context.User) &&
            !MfaPrincipal.HasState(context.User, MfaClaimValues.MfaInProgress));

    /// <summary>Must be checked against the final authorized user before reading or modifying factor data.</summary>
    public static bool CanManage(ClaimsPrincipal user, bool factorEnabled, bool mfaEnforced, MfaClaimConfig config,
        string? subjectClaimType = null)
    {
        if (!MfaPrincipal.HasSingleAccount(user, subjectClaimType) || MfaPrincipal.HasState(user, MfaClaimValues.MfaInProgress))
            return false;
        if (MfaPrincipal.IsCompleted(user, config))
            return true;
        return !factorEnabled && (!mfaEnforced || MfaPrincipal.HasState(user, MfaClaimValues.MfaSetupRequired));
    }
}
