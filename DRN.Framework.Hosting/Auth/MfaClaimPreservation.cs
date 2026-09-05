using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Framework.Hosting.Auth;

internal static class MfaClaimPreservation
{
    internal static void Preserve(ClaimsPrincipal source, ClaimsIdentity target, MfaClaimConfig config)
    {
        foreach (var identity in source.Identities)
        {
            if (!identity.IsAuthenticated)
                continue;

            foreach (var claim in identity.Claims)
            {
                var isAmr = string.Equals(claim.Type, ClaimConventions.AuthenticationMethodReference, StringComparison.OrdinalIgnoreCase);
                var isConfiguredMfa = string.Equals(claim.Type, config.ClaimType, StringComparison.OrdinalIgnoreCase) && claim.Value == config.ClaimValue;

                if ((isAmr || isConfiguredMfa) && !target.HasClaim(claim.Type, claim.Value))
                    target.AddClaim(new Claim(claim.Type, claim.Value));
            }
        }
    }
}
