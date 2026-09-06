using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Logging;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Auth;

[Singleton<IAuthorizationMiddlewareResultHandler>(tryAdd: false)]
public sealed class MfaEnforcingAuthorizationMiddlewareResultHandler(
    IOptions<AuthorizationOptions> authorizationOptions,
    AuthenticationClaimConfig? claimConfig = null,
    MfaExemptionOptions? exemptionOptions = null) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly bool _mfaNotEnforced = !MfaAuthorization.IsMfaEnforced(authorizationOptions.Value);
    private readonly AuthenticationClaimConfig _claimConfig = claimConfig ?? AuthenticationClaimConfig.Default;
    private readonly MfaExemptionOptions _exemptionOptions = exemptionOptions ?? new MfaExemptionOptions();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        // TODO(MFA-06): Connect factor, recovery, and revocation events at their owners.
        await SynchronizePolicyProofsAsync(context, policy);

        if (!authorizeResult.Succeeded || _mfaNotEnforced || MfaAuthorization.IsPolicyMfaExempt(policy))
        {
            if (!_mfaNotEnforced)
                Audit(context, authorizeResult, GetAuditReason(authorizeResult));
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var result = authorizeResult;
        if (!MfaPolicyProof.IsSatisfied(context, context.User, _claimConfig, _exemptionOptions))
            result = AuthenticationFor.IsAuthenticated(context.User)
                ? PolicyAuthorizationResult.Forbid()
                : PolicyAuthorizationResult.Challenge();

        if (!result.Succeeded)
            Audit(context, result, "mfa_required");
        else if (!MfaPrincipal.IsCompleted(context.User, _claimConfig))
            Audit(context, result, "scheme_exemption");

        await _defaultHandler.HandleAsync(next, context, policy, result);
    }

    private async Task SynchronizePolicyProofsAsync(HttpContext context, AuthorizationPolicy policy)
    {
        // Bind evidence to the evaluated policy before projecting it, including on exempt policies.
        var proofs = MfaPolicyProof.Get(context);
        if (proofs.Count > 0)
        {
            var schemes = await MfaPolicyProof.GetSchemesAsync(context, policy);
            proofs = proofs.Where(proof => schemes.Contains(proof.SelectedScheme, StringComparer.Ordinal)).ToArray();
            MfaPolicyProof.SetSelected(context, proofs);
        }

        if (context.RequestServices.GetService<IScopedUser>() is ScopedUser scopedUser)
        {
            scopedUser.SetUser(context.User);
            if (proofs.Count == 1 && MfaAuthorization.IsMfaSatisfied(context.User, _claimConfig, _exemptionOptions,
                    proofs[0].Proof.Scheme, proofs[0].Proof.Principal))
                scopedUser.SetExemption(proofs[0].Proof.Scheme, proofs[0].Proof.Principal);
        }
    }

    private static string GetAuditReason(PolicyAuthorizationResult result)
    {
        if (result.Succeeded)
            return "policy_exemption";

        var failure = result.AuthorizationFailure;
        return failure?.FailedRequirements.Any(requirement => requirement is MfaRequirement) == true ||
               failure?.FailureReasons.Any(reason => reason.Handler is RequireMfaHandler) == true
            ? "mfa_requirement"
            : "authorization";
    }

    private static void Audit(HttpContext context, PolicyAuthorizationResult result, string reason)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<MfaEnforcingAuthorizationMiddlewareResultHandler>>();
        var scopedLog = context.RequestServices.GetRequiredService<IScopedLog>();

        var eventId = HostingLogEvents.MfaAuthorizationExemption;
        var outcome = "exemption";
        if (result.Challenged)
        {
            eventId = HostingLogEvents.MfaAuthorizationChallenge;
            outcome = "challenge";
        }
        else if (result.Forbidden)
        {
            eventId = HostingLogEvents.MfaAuthorizationForbid;
            outcome = "forbid";
        }

        scopedLog.WithEvent(new ScopeEvent(eventId, outcome, reason));
        var traceId = scopedLog.TraceId;
        var correlationId = scopedLog.CorrelationId;
        logger.LogInformation(eventId, "Authorization decision {EventOutcome} ({EventReason}); trace {TraceId}; correlation {CorrelationId}",
            outcome, reason, traceId, correlationId);
    }
}
