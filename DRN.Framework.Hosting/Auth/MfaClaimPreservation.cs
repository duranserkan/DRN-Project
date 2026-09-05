using System.Globalization;
using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Framework.Hosting.Auth;

internal static class MfaClaimPreservation
{
    private const string AuthenticationTime = "auth_time";

    internal static void Preserve(ClaimsPrincipal source, ClaimsIdentity target, MfaClaimConfig config)
    {
        foreach (var identity in source.Identities)
        {
            if (!identity.IsAuthenticated)
                continue;

            foreach (var claim in identity.Claims)
            {
                if (string.Equals(claim.Type, AuthenticationTime, StringComparison.OrdinalIgnoreCase))
                    continue;

                var isAmr = string.Equals(claim.Type, ClaimConventions.AuthenticationMethodReference, StringComparison.OrdinalIgnoreCase);
                var isConfiguredMfa = string.Equals(claim.Type, config.ClaimType, StringComparison.OrdinalIgnoreCase) && claim.Value == config.ClaimValue;

                if ((isAmr || isConfiguredMfa) && !target.Claims.Any(existing =>
                        string.Equals(existing.Type, claim.Type, StringComparison.OrdinalIgnoreCase) &&
                        existing.Value == claim.Value && existing.ValueType == claim.ValueType &&
                        existing.Issuer == claim.Issuer && existing.OriginalIssuer == claim.OriginalIssuer))
                    target.AddClaim(claim.Clone(target));
            }
        }

        PreserveAuthenticationTime(source, target, config);
    }

    private static void PreserveAuthenticationTime(ClaimsPrincipal source, ClaimsIdentity target, MfaClaimConfig config)
    {
        var identities = source.Identities.Where(identity => identity.IsAuthenticated).ToArray();
        var timestamps = identities.SelectMany(identity => identity.FindAll(AuthenticationTime)).ToArray();

        // A principal factory may issue today's timestamp. Renewal must retain the original evidence or none.
        foreach (var claim in target.FindAll(AuthenticationTime).ToArray())
            target.RemoveClaim(claim);

        var subject = Subject(target);
        if (!target.IsAuthenticated || subject == null || identities.Length == 0 ||
            identities.Any(identity => Subject(identity) is not { } candidate ||
                                       candidate.Value != subject.Value || candidate.Issuer != subject.Issuer) ||
            timestamps.Length == 0)
            return;

        var original = timestamps[0];
        if (!long.TryParse(original.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds() ||
            timestamps.Any(claim => claim.Value != original.Value || claim.ValueType != original.ValueType ||
                                    claim.Issuer != original.Issuer || claim.OriginalIssuer != original.OriginalIssuer))
            return;

        // Flattening identities must not turn separate authentication and MFA evidence into a pair.
        // Include factory-issued markers on the target, not just markers copied from the source.
        if (target.Claims.Any(claim => claim.Type == config.ClaimType && claim.Value == config.ClaimValue &&
                                      claim.Issuer == original.Issuer) &&
            !identities.Any(identity =>
                identity.Claims.Any(claim => claim.Type == AuthenticationTime && claim.Value == original.Value) &&
                identity.Claims.Any(claim => claim.Type == config.ClaimType && claim.Value == config.ClaimValue &&
                                             claim.Issuer == original.Issuer)))
            return;

        target.AddClaim(original.Clone(target));
    }

    private static Claim? Subject(ClaimsIdentity identity)
    {
        var subjects = identity.Claims.Where(claim => claim.Type == ClaimConventions.NameIdentifier || claim.Type == "sub").ToArray();
        var subject = subjects.FirstOrDefault();
        return subject != null && !string.IsNullOrWhiteSpace(subject.Value) &&
               subjects.All(claim => claim.Value == subject.Value && claim.Issuer == subject.Issuer)
            ? subject
            : null;
    }
}
