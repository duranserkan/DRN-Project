using System.Collections.Frozen;
using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Auth.Policies;

public class MfaRequirement : IAuthorizationRequirement;

[Singleton<IAuthorizationHandler>(tryAdd: false)]
public class RequireMfaHandler(AuthenticationClaimConfig? claimConfig = null, MfaExemptionOptions? exemptionOptions = null)
    : AuthorizationHandler<MfaRequirement>
{
    private readonly AuthenticationClaimConfig _claimConfig = claimConfig ?? AuthenticationClaimConfig.Default;
    private readonly MfaExemptionOptions _exemptionOptions = exemptionOptions ?? new MfaExemptionOptions();

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MfaRequirement requirement)
    {
        if (MfaPolicyProof.IsSatisfied(context.Resource as HttpContext, context.User, _claimConfig, _exemptionOptions))
            context.Succeed(requirement);
        else
            context.Fail(new AuthorizationFailureReason(this, "mfa_required"));
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

/// <summary>Enforces completed MFA using the selected principal and policy-bound exemption evidence.</summary>
/// <remarks>
/// MFA delivery phases (addressed means implementation/test coverage exists, not that tests were executed):
/// Phase 1:
/// - ADDRESSED MFA-04 renewal: preserve original account-bound auth_time in cookies and bearer refresh.
/// - ADDRESSED MFA-06 result-handler auditing: correlated challenge, forbid and effective exemption events.
/// - DEFERRED MFA-01: fresh, replay-resistant proof before enrolled-factor changes.
/// Phase 2:
/// - ADDRESSED MFA-02: revocation contract documented with controlled-clock coverage in MfaRevocationTests.
/// - ADDRESSED MFA-04 assurance: opt-in MfaPrincipal.IsRecent/IsPhishingResistant; existing Mfa policy unchanged.
/// Remaining work:
/// - TODO(MFA-01): Atomic replay prevention, attempt limits and client-compatible factor-management step-up.
/// - TODO(MFA-03): Lost-factor/admin recovery controls, single-use recovery credentials and notifications.
/// - TODO(MFA-05): Passkey enrollment/authentication, verified user verification and downgrade-resistant recovery.
/// - TODO(MFA-07): Provider-specific trusted OIDC mappings, evidence issuance and interoperability tests.
/// - TODO(MFA-06): Factor, recovery and revocation audit events at their owning operations.
/// </remarks>
public static class MfaAuthorization
{
    public static bool IsMfaEnforced(AuthorizationOptions options) => options.DefaultPolicy.Requirements.Any(r => r is MfaRequirement);

    internal static bool IsPolicyMfaExempt(AuthorizationPolicy policy) =>
        policy.Requirements.Any(requirement => requirement is MfaExemptRequirement);

    public static bool IsMfaSatisfied(ClaimsPrincipal? user, AuthenticationClaimConfig claimConfig, MfaExemptionOptions exemptionOptions,
        string? authenticatedExemptionScheme, ClaimsPrincipal? exemptionPrincipal)
    {
        if (user == null || MfaPrincipal.IsRestricted(user) || !MfaPrincipal.HasSingleAccount(user, claimConfig))
            return false;
        if (MfaPrincipal.IsCompleted(user, claimConfig))
            return true;
        if (string.IsNullOrWhiteSpace(authenticatedExemptionScheme) ||
            !exemptionOptions.ExemptAuthSchemes.Contains(authenticatedExemptionScheme) ||
            exemptionPrincipal == null || MfaPrincipal.IsRestricted(exemptionPrincipal))
            return false;

        return user.Identities.Any(identity => exemptionPrincipal.Identities.Any(proof =>
            MfaPrincipal.MatchesIdentity(identity, proof, claimConfig)));
    }
}
