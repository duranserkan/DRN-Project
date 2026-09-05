using System.Collections.Frozen;
using System.Security.Claims;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Auth.Policies;

public class MfaRequirement : IAuthorizationRequirement;

[Singleton<IAuthorizationHandler>(tryAdd: false)]
public class RequireMfaHandler(MfaClaimConfig? claimConfig = null, MfaExemptionOptions? exemptionOptions = null) : AuthorizationHandler<MfaRequirement>
{
    private readonly MfaClaimConfig _claimConfig = claimConfig ?? MfaClaimConfig.AspNetIdentity;
    private readonly MfaExemptionOptions _exemptionOptions = exemptionOptions ?? new MfaExemptionOptions();

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MfaRequirement requirement)
    {
        if (MfaPolicyProof.IsSatisfied(context.Resource as HttpContext, context.User, _claimConfig, _exemptionOptions))
            context.Succeed(requirement);
        else
            context.Fail();
        return Task.CompletedTask;
    }
}

/// <summary>Eligible schemes; only schemes selected by the endpoint policy can supply exemption evidence.</summary>
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
    public static bool IsMfaEnforced(AuthorizationOptions options) => options.DefaultPolicy.Requirements.Any(r => r is MfaRequirement);

    internal static bool IsPolicyMfaExempt(AuthorizationPolicy policy) =>
        policy.Requirements.Any(requirement => requirement is MfaExemptRequirement);

    public static bool IsMfaSatisfied(ClaimsPrincipal? user, MfaClaimConfig claimConfig, MfaExemptionOptions exemptionOptions,
        string? authenticatedExemptionScheme, ClaimsPrincipal? exemptionPrincipal)
    {
        if (user == null || MfaPrincipal.IsRestricted(user) || !MfaPrincipal.HasSingleAccount(user))
            return false;
        if (MfaPrincipal.IsCompleted(user, claimConfig))
            return true;
        if (string.IsNullOrWhiteSpace(authenticatedExemptionScheme) ||
            !exemptionOptions.ExemptAuthSchemes.Contains(authenticatedExemptionScheme) ||
            exemptionPrincipal == null || MfaPrincipal.IsRestricted(exemptionPrincipal))
            return false;

        return user.Identities.Any(identity => exemptionPrincipal.Identities.Any(proof =>
            MfaPrincipal.MatchesIdentity(identity, proof)));
    }
}
