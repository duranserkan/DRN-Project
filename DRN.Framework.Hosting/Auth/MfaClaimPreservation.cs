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
        RemoveCeremonyClaims(target, config.Mfa);

        // Validate the whole source before flattening identities: another account's MFA marker
        // must not become evidence for the renewed account, even when no timestamp is present.
        if (!HasMatchingSubject(target, identities, config))
            return false;

        CopyMfaClaims(identities, target, config.Mfa);
        PreserveAuthenticationTime(identities, target, config.Mfa);
        return true;
    }

    private static void RemoveCeremonyClaims(ClaimsIdentity target, MfaClaimConfig config)
    {
        foreach (var claim in target.Claims.Where(claim => IsCeremonyClaim(claim, config)).ToArray())
            target.RemoveClaim(claim);
    }

    private static bool IsCeremonyClaim(Claim claim, MfaClaimConfig config) =>
        string.Equals(claim.Type, AuthenticationTime, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claim.Type, AuthClaimTypes.AuthenticationMethods, StringComparison.OrdinalIgnoreCase) ||
        claim.Type == ClaimTypes.AuthenticationMethod ||
        claim.Type == config.ClaimType && claim.Value == config.ClaimValue;

    private static bool HasMatchingSubject(ClaimsIdentity target, ClaimsIdentity[] identities, AuthenticationClaimConfig config)
    {
        var subject = SubjectClaims.Find(target, config);
        if (!target.IsAuthenticated || subject == null || identities.Length == 0)
            return false;

        return identities.All(identity =>
            SubjectClaims.Find(identity, config) is { } candidate &&
            candidate.Value == subject.Value &&
            candidate.Issuer == subject.Issuer);
    }

    private static void CopyMfaClaims(ClaimsIdentity[] identities, ClaimsIdentity target, MfaClaimConfig config)
    {
        foreach (var identity in identities)
        {
            foreach (var claim in identity.Claims)
                if (IsTransferableMfaClaim(claim, config) && !HasEquivalentClaim(target, claim))
                    target.AddClaim(claim.Clone(target));
        }
    }

    private static bool IsTransferableMfaClaim(Claim claim, MfaClaimConfig config)
    {
        if (string.Equals(claim.Type, AuthenticationTime, StringComparison.OrdinalIgnoreCase))
            return false;

        var isAmr = string.Equals(claim.Type, ClaimConventions.AuthenticationMethodReference, StringComparison.OrdinalIgnoreCase);
        var isConfiguredMfa = claim.Type == config.ClaimType && claim.Value == config.ClaimValue;

        return isAmr || isConfiguredMfa || claim.Type == ClaimTypes.AuthenticationMethod;
    }

    private static bool HasEquivalentClaim(ClaimsIdentity target, Claim claim) =>
        target.Claims.Any(existing =>
            existing.Type == claim.Type &&
            existing.Value == claim.Value &&
            existing.ValueType == claim.ValueType &&
            existing.Issuer == claim.Issuer &&
            existing.OriginalIssuer == claim.OriginalIssuer &&
            ArePropertiesEqual(existing, claim));

    private static bool ArePropertiesEqual(Claim a, Claim b) =>
        a.Properties.Count == b.Properties.Count &&
        a.Properties.All(pair => b.Properties.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static void PreserveAuthenticationTime(ClaimsIdentity[] identities, ClaimsIdentity target, MfaClaimConfig config)
    {
        var timestamps = identities.SelectMany(identity => identity.FindAll(AuthenticationTime)).ToArray();
        if (timestamps.Length == 0)
            return;

        var original = timestamps[0];
        if (!IsValidAuthenticationTime(original) || !HaveMatchingTimestamps(timestamps, original))
            return;

        // Flattening identities must not turn separate authentication and MFA evidence into a pair.
        if (HasMfaFromIssuer(target, config, original.Issuer) && !HasPairedEvidence(identities, original, config))
            return;

        target.AddClaim(original.Clone(target));
    }

    private static bool IsValidAuthenticationTime(Claim timestamp) =>
        long.TryParse(timestamp.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
        seconds <= DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    private static bool HaveMatchingTimestamps(Claim[] timestamps, Claim original) =>
        timestamps.All(claim =>
            claim.Value == original.Value &&
            claim.ValueType == original.ValueType &&
            claim.Issuer == original.Issuer &&
            claim.OriginalIssuer == original.OriginalIssuer &&
            ArePropertiesEqual(claim, original));

    private static bool HasPairedEvidence(ClaimsIdentity[] identities, Claim timestamp, MfaClaimConfig config) =>
        identities.Any(identity =>
            identity.Claims.Any(claim => claim.Type == AuthenticationTime && claim.Value == timestamp.Value) &&
            HasMfaFromIssuer(identity, config, timestamp.Issuer));

    private static bool HasMfaFromIssuer(ClaimsIdentity identity, MfaClaimConfig config, string issuer) =>
        identity.Claims.Any(claim => claim.Type == config.ClaimType && claim.Value == config.ClaimValue && claim.Issuer == issuer);
}
