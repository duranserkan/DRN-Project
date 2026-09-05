using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Middlewares;

public class MfaExemptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedUser scopedUser, MfaExemptionOptions exemptionOptions)
    {
        var concreteUser = scopedUser as ScopedUser;
        concreteUser?.SetExemption(null);
        var proofs = new List<(string SelectedScheme, ExemptionProof Proof)>();
        MfaPolicyProof.SetSelected(httpContext, proofs);

        var policy = await MfaPolicyProof.ResolvePolicyAsync(httpContext);
        if (policy != null && exemptionOptions.ExemptAuthSchemes.Count > 0)
        {
            foreach (var scheme in await MfaPolicyProof.GetSchemesAsync(httpContext, policy))
            {
                var proof = await AuthenticateExemptionAsync(httpContext, scheme, exemptionOptions);
                if (proof != null)
                    proofs.Add((scheme, proof));
            }
        }

        // Compatibility projection only. Authorization uses all policy-selected proofs.
        if (proofs.Count == 1)
            concreteUser?.SetExemption(proofs[0].Proof.Scheme, proofs[0].Proof.Principal);

        await next(httpContext);
    }

    private static async Task<ExemptionProof?> AuthenticateExemptionAsync(
        HttpContext context, string scheme, MfaExemptionOptions exemptionOptions)
    {
        var result = await context.AuthenticateAsync(scheme);
        var claims = context.RequestServices.GetService<AuthenticationClaimConfig>();
        if (result is not { Succeeded: true, Principal: not null } ||
            !MfaPrincipal.HasSingleAccount(result.Principal, claims) || MfaPrincipal.IsRestricted(result.Principal))
            return null;

        // A selected forwarding scheme may authenticate through an eligible concrete handler.
        var authenticatedScheme = exemptionOptions.ExemptAuthSchemes.Contains(scheme)
            ? scheme : result.Ticket?.AuthenticationScheme;
        return authenticatedScheme != null && exemptionOptions.ExemptAuthSchemes.Contains(authenticatedScheme)
            ? new ExemptionProof(authenticatedScheme, result.Principal)
            : null;
    }
}
