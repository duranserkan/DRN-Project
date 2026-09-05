using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Auth;

[Singleton<IAuthorizationMiddlewareResultHandler>(tryAdd: false)]
public sealed class MfaEnforcingAuthorizationMiddlewareResultHandler(
    IOptions<AuthorizationOptions> authorizationOptions,
    MfaClaimConfig? claimConfig = null,
    MfaExemptionOptions? exemptionOptions = null) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly bool _mfaNotEnforced = !MfaAuthorization.IsMfaEnforced(authorizationOptions.Value);
    private readonly MfaClaimConfig _claimConfig = claimConfig ?? MfaClaimConfig.AspNetIdentity;
    private readonly MfaExemptionOptions _exemptionOptions = exemptionOptions ?? new MfaExemptionOptions();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        // Bind evidence to the evaluated policy before projecting it, including on exempt policies.
        var proofs = MfaPolicyProof.Get(context);
        if (proofs.Count > 0)
        {
            var schemes = await MfaPolicyProof.GetSchemesAsync(context, policy);
            proofs = proofs.Where(proof => schemes.Contains(proof.SelectedScheme, StringComparer.Ordinal)).ToArray();
            MfaPolicyProof.SetSelected(context, proofs);
        }

        if (context.RequestServices?.GetService<IScopedUser>() is ScopedUser scopedUser)
        {
            scopedUser.SetUser(context.User);
            if (proofs.Count == 1 && MfaAuthorization.IsMfaSatisfied(context.User, _claimConfig, _exemptionOptions,
                    proofs[0].Proof.Scheme, proofs[0].Proof.Principal))
                scopedUser.SetExemption(proofs[0].Proof.Scheme, proofs[0].Proof.Principal);
        }

        if (!authorizeResult.Succeeded || _mfaNotEnforced || MfaAuthorization.IsPolicyMfaExempt(policy))
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var result = MfaPolicyProof.IsSatisfied(context, context.User, _claimConfig, _exemptionOptions)
            ? authorizeResult
            : AuthenticationFor.IsAuthenticated(context.User)
                ? PolicyAuthorizationResult.Forbid()
                : PolicyAuthorizationResult.Challenge();

        await _defaultHandler.HandleAsync(next, context, policy, result);
    }
}
