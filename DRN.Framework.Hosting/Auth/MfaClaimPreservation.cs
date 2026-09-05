using System.Globalization;
using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Framework.Hosting.Auth;

internal static class MfaClaimPreservation
{
    private const string AuthenticationTime = AuthClaimTypes.AuthenticationTime;

    internal static bool Preserve(ClaimsPrincipal source, ClaimsPrincipal target, AuthenticationClaimConfig config) =>
        target.Identities.Any() && target.Identities.All(identity => Preserve(source, identity, config));

    internal static bool Preserve(ClaimsPrincipal source, ClaimsIdentity target, AuthenticationClaimConfig config)
    {
        var identities = source.Identities.Where(identity => identity.IsAuthenticated).ToArray();

        // A factory describes the account, not the original authentication ceremony.
        foreach (var claim in target.Claims.Where(claim =>
                     string.Equals(claim.Type, AuthenticationTime, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(claim.Type, AuthClaimTypes.AuthenticationMethods, StringComparison.OrdinalIgnoreCase) ||
                     claim.Type == ClaimTypes.AuthenticationMethod ||
                     claim.Type == config.Mfa.ClaimType && claim.Value == config.Mfa.ClaimValue).ToArray())
            target.RemoveClaim(claim);

        // Validate the whole source before flattening identities: another account's MFA marker
        // must not become evidence for the renewed account, even when no timestamp is present.
        var subject = SubjectClaims.Find(target, config);
        if (!target.IsAuthenticated || subject == null || identities.Length == 0 ||
            identities.Any(identity => SubjectClaims.Find(identity, config) is not { } candidate ||
                                       candidate.Value != subject.Value || candidate.Issuer != subject.Issuer))
            return false;

        foreach (var identity in identities)
        {
            foreach (var claim in identity.Claims)
            {
                if (string.Equals(claim.Type, AuthenticationTime, StringComparison.OrdinalIgnoreCase))
                    continue;

                var isAmr = string.Equals(claim.Type, ClaimConventions.AuthenticationMethodReference, StringComparison.OrdinalIgnoreCase);
                var isConfiguredMfa = claim.Type == config.Mfa.ClaimType && claim.Value == config.Mfa.ClaimValue;

                if ((isAmr || isConfiguredMfa || claim.Type == ClaimTypes.AuthenticationMethod) && !target.Claims.Any(existing =>
                        existing.Type == claim.Type &&
                        existing.Value == claim.Value && existing.ValueType == claim.ValueType &&
                        existing.Issuer == claim.Issuer && existing.OriginalIssuer == claim.OriginalIssuer &&
                        existing.Properties.Count == claim.Properties.Count &&
                        existing.Properties.All(pair => claim.Properties.TryGetValue(pair.Key, out var value) && value == pair.Value)))
                    target.AddClaim(claim.Clone(target));
            }
        }

        PreserveAuthenticationTime(identities, target, config.Mfa);
        return true;
    }

    private static void PreserveAuthenticationTime(ClaimsIdentity[] identities, ClaimsIdentity target, MfaClaimConfig config)
    {
        var timestamps = identities.SelectMany(identity => identity.FindAll(AuthenticationTime)).ToArray();
        if (timestamps.Length == 0)
            return;

        var original = timestamps[0];
        if (!long.TryParse(original.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds() ||
            timestamps.Any(claim => claim.Value != original.Value || claim.ValueType != original.ValueType ||
                                    claim.Issuer != original.Issuer || claim.OriginalIssuer != original.OriginalIssuer ||
                                    claim.Properties.Count != original.Properties.Count ||
                                    claim.Properties.Any(pair => !original.Properties.TryGetValue(pair.Key, out var value) || value != pair.Value)))
            return;

        // Flattening identities must not turn separate authentication and MFA evidence into a pair.
        if (target.Claims.Any(claim => claim.Type == config.ClaimType && claim.Value == config.ClaimValue &&
                                      claim.Issuer == original.Issuer) &&
            !identities.Any(identity =>
                identity.Claims.Any(claim => claim.Type == AuthenticationTime && claim.Value == original.Value) &&
                identity.Claims.Any(claim => claim.Type == config.ClaimType && claim.Value == config.ClaimValue &&
                                             claim.Issuer == original.Issuer)))
            return;

        target.AddClaim(original.Clone(target));
    }

}
