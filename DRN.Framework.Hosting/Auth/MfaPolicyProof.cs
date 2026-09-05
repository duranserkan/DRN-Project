using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Auth;

/// <summary>Request-local evidence from schemes selected by the effective endpoint policy.</summary>
internal static class MfaPolicyProof
{
    private static readonly object ProofKey = new();

    internal static async Task<AuthorizationPolicy?> ResolvePolicyAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
            return null;

        var provider = context.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorizeData = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>() ?? [];
        var authorizationPolicy = endpoint?.Metadata.GetOrderedMetadata<AuthorizationPolicy>() ?? [];

        var policy = await AuthorizationPolicy.CombineAsync(provider, authorizeData, authorizationPolicy);
        var requirements = endpoint?.Metadata.GetOrderedMetadata<IAuthorizationRequirementData>() ?? [];
        if (requirements.Count == 0)
            return policy;

        var builder = new AuthorizationPolicyBuilder();
        foreach (var data in requirements)
            builder.AddRequirements([.. data.GetRequirements()]);

        return policy == null ? builder.Build() : AuthorizationPolicy.Combine(policy, builder.Build());
    }

    internal static async Task<IReadOnlyList<string>> GetSchemesAsync(HttpContext context, AuthorizationPolicy policy)
    {
        if (policy.AuthenticationSchemes.Count > 0)
            return policy.AuthenticationSchemes;

        var provider = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await provider.GetDefaultAuthenticateSchemeAsync();
        return scheme == null ? [] : [scheme.Name];
    }

    internal static void Set(HttpContext context, IReadOnlyList<ExemptionProof> proofs) =>
        SetSelected(context, proofs.Select(proof => (proof.Scheme, proof)).ToArray());

    internal static void SetSelected(HttpContext context, IReadOnlyList<(string SelectedScheme, ExemptionProof Proof)> proofs) =>
        context.Items[ProofKey] = proofs;

    internal static IReadOnlyList<(string SelectedScheme, ExemptionProof Proof)> Get(HttpContext context) =>
        context.Items.TryGetValue(ProofKey, out var value) && value is IReadOnlyList<(string, ExemptionProof)> proofs ? proofs : [];

    internal static bool IsSatisfied(HttpContext? context, ClaimsPrincipal user, MfaClaimConfig config, MfaExemptionOptions options) =>
        MfaPrincipal.IsCompleted(user, config) || context != null && Get(context).Any(proof =>
            MfaAuthorization.IsMfaSatisfied(user, config, options, proof.Proof.Scheme, proof.Proof.Principal));
}
