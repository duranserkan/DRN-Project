using System.Collections.Frozen;
using System.Security.Claims;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authorization;

namespace DRN.Framework.Hosting.Auth.Policies;

public class MfaRequirement : IAuthorizationRequirement;

[Singleton<IAuthorizationHandler>(tryAdd: false)]
public class RequireMfaHandler(MfaClaimConfig? claimConfig = null, MfaExemptionOptions? exemptionOptions = null) : AuthorizationHandler<MfaRequirement>
{
    private readonly MfaClaimConfig _claimConfig = claimConfig ?? MfaClaimConfig.AspNetIdentity;
    private readonly MfaExemptionOptions _exemptionOptions = exemptionOptions ?? new MfaExemptionOptions();

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MfaRequirement requirement)
    {
        var scopedUser = ScopeContext.User;
        if (MfaAuthorization.IsMfaSatisfied(context.User, _claimConfig, _exemptionOptions, scopedUser.ExemptionScheme, scopedUser.ExemptionPrincipal))
            context.Succeed(requirement);
        else
            context.Fail();

        return Task.CompletedTask;
    }
}

/// <summary>
/// Required to configure MFA Exemption. When provided by <see cref="DrnProgramBase{TProgram}.ConfigureMFAExemption"/>,
/// specifies the authentication schemes that are exempt from multi-factor enforcement.
/// </summary>
public class MfaExemptionConfig
{
    public IReadOnlyList<string> ExemptAuthSchemes { get; init; } = [];
}

[Singleton<MfaExemptionOptions>]
public class MfaExemptionOptions
{
    public FrozenSet<string> ExemptAuthSchemes { get; private set; } = FrozenSet<string>.Empty;

    internal void MapFromConfig(MfaExemptionConfig config) => ExemptAuthSchemes = config.ExemptAuthSchemes.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}

public static class MfaAuthorization
{
    public static bool IsMfaEnforced(AuthorizationOptions options)
        => options.DefaultPolicy.Requirements.Any(r => r is MfaRequirement);

    internal static bool IsPolicyMfaExempt(AuthorizationPolicy policy)
        => policy.Requirements.Any(requirement => requirement is MfaExemptRequirement);

    public static bool IsMfaSatisfied(
        ClaimsPrincipal? user,
        MfaClaimConfig claimConfig,
        MfaExemptionOptions exemptionOptions,
        string? authenticatedExemptionScheme,
        ClaimsPrincipal? exemptionPrincipal)
    {
        if (user == null)
            return false;

        var hasAuthenticatedIdentity = false;
        var isMfaCompleted = false;

        foreach (var identity in user.Identities)
        {
            if (!identity.IsAuthenticated)
                continue;

            hasAuthenticatedIdentity = true;

            foreach (var claim in identity.Claims)
            {
                if (string.Equals(claim.Type, ClaimConventions.AuthenticationMethod, StringComparison.OrdinalIgnoreCase) && claim.Value == MfaClaimValues.MfaSetupRequired)
                    return false;

                if (string.Equals(claim.Type, claimConfig.ClaimType, StringComparison.OrdinalIgnoreCase) && claim.Value == claimConfig.ClaimValue)
                    isMfaCompleted = true;
            }
        }

        if (!hasAuthenticatedIdentity)
            return false;

        if (isMfaCompleted)
            return true;

        if (exemptionOptions.ExemptAuthSchemes.Count == 0 || exemptionPrincipal == null)
            return false;

        var effectiveScheme = authenticatedExemptionScheme ?? GetFirstAuthenticatedScheme(exemptionPrincipal);

        if (string.IsNullOrWhiteSpace(effectiveScheme) || !exemptionOptions.ExemptAuthSchemes.Contains(effectiveScheme))
            return false;

        // Fast-path for requests with a single authenticated identity on both sides
        if (user.Identity is ClaimsIdentity { IsAuthenticated: true } singleUserIdentity &&
            exemptionPrincipal.Identity is ClaimsIdentity { IsAuthenticated: true } singleExemptIdentity &&
            (ReferenceEquals(singleUserIdentity, singleExemptIdentity) || IsMatchingTransformedIdentity(singleUserIdentity, singleExemptIdentity, effectiveScheme)))
            return true;

        // Fallback for multi-identity principals
        foreach (var userIdentity in user.Identities)
        {
            if (!userIdentity.IsAuthenticated)
                continue;

            foreach (var exemptIdentity in exemptionPrincipal.Identities)
            {
                if (!exemptIdentity.IsAuthenticated)
                    continue;

                if (ReferenceEquals(userIdentity, exemptIdentity) || IsMatchingTransformedIdentity(userIdentity, exemptIdentity, effectiveScheme))
                    return true;
            }
        }

        return false;
    }

    private static string? GetFirstAuthenticatedScheme(ClaimsPrincipal principal)
    {
        if (principal.Identity is ClaimsIdentity { IsAuthenticated: true } primary && !string.IsNullOrWhiteSpace(primary.AuthenticationType))
            return primary.AuthenticationType;

        foreach (var identity in principal.Identities)
            if (identity.IsAuthenticated && !string.IsNullOrWhiteSpace(identity.AuthenticationType))
                return identity.AuthenticationType;

        return null;
    }

    private static bool IsMatchingTransformedIdentity(ClaimsIdentity userIdentity, ClaimsIdentity exemptIdentity, string exemptScheme)
    {
        if (!string.Equals(userIdentity.AuthenticationType, exemptScheme, StringComparison.OrdinalIgnoreCase))
            return false;

        var exemptNameId = exemptIdentity.FindFirst(ClaimConventions.NameIdentifier)?.Value;
        return exemptNameId == null || string.Equals(userIdentity.FindFirst(ClaimConventions.NameIdentifier)?.Value, exemptNameId, StringComparison.Ordinal);
    }
}
