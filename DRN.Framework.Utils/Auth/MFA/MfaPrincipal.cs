using System.Globalization;
using System.Security.Claims;

namespace DRN.Framework.Utils.Auth.MFA;

/// <summary>Evaluates MFA evidence without depending on an identity provider.</summary>
public static class MfaPrincipal
{
    public static bool HasState(ClaimsPrincipal? principal, string state) =>
        principal?.Identities.Any(identity => identity.IsAuthenticated &&
                                              identity.HasClaim(ClaimConventions.AuthenticationMethod, state)) == true;

    public static bool IsRestricted(ClaimsPrincipal? principal) =>
        HasState(principal, MfaClaimValues.MfaSetupRequired) || HasState(principal, MfaClaimValues.MfaInProgress);

    public static bool IsCompleted(ClaimsPrincipal? principal, MfaClaimConfig config) =>
        principal != null && !IsRestricted(principal) && HasSingleAccount(principal) &&
        principal.Identities.Any(identity => identity.IsAuthenticated && identity.HasClaim(config.ClaimType, config.ClaimValue));

    /// <summary>Checks completed MFA and recent authentication evidence from an explicitly trusted issuer.</summary>
    /// <remarks>
    /// The completed marker and timestamp must belong to the same authenticated identity and issuer.
    /// The age boundary is inclusive; future, missing, malformed and conflicting timestamps fail closed.
    /// auth_time describes authentication, not necessarily the last MFA verification. For recent MFA,
    /// select a timestamp whose verified-MFA meaning is guaranteed by the issuing provider.
    /// This helper does not change the default Mfa policy or issue evidence.
    /// </remarks>
    public static bool IsRecent(ClaimsPrincipal? principal, MfaClaimConfig config, string trustedIssuer,
        TimeSpan maximumAge, DateTimeOffset utcNow, string authenticationTimeClaimType = "auth_time")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationTimeClaimType);
        if (maximumAge < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximumAge));

        return AssuranceIdentities(principal, config, trustedIssuer).Any(identity =>
        {
            var timestamps = identity.Claims.Where(claim => claim.Type == authenticationTimeClaimType).ToArray();
            if (timestamps.Length == 0)
                return false;
            var timestamp = timestamps[0];
            if (timestamps.Any(claim => claim.Issuer != trustedIssuer || claim.Value != timestamp.Value ||
                                        claim.ValueType != timestamp.ValueType || claim.OriginalIssuer != timestamp.OriginalIssuer) ||
                !long.TryParse(timestamp.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
                seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
                return false;

            var authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return authenticatedAt <= utcNow && utcNow - authenticatedAt <= maximumAge;
        });
    }

    /// <summary>Checks an explicit provider-guaranteed phishing-resistant assurance marker alongside completed MFA.</summary>
    /// <remarks>
    /// Both markers must come from the trusted issuer on the same authenticated identity. The assurance
    /// marker must differ from the completed marker; generic completed MFA alone is insufficient.
    /// The caller owns the provider's assurance mapping; no method name or passkey label is inferred.
    /// </remarks>
    public static bool IsPhishingResistant(ClaimsPrincipal? principal, MfaClaimConfig config, string trustedIssuer,
        MfaClaimConfig assuranceClaim)
    {
        ArgumentNullException.ThrowIfNull(assuranceClaim);
        var identities = AssuranceIdentities(principal, config, trustedIssuer);
        if (config.ClaimType == assuranceClaim.ClaimType && config.ClaimValue == assuranceClaim.ClaimValue)
            return false;

        return identities.Any(identity => identity.Claims.Any(claim => claim.Type == assuranceClaim.ClaimType &&
            claim.Value == assuranceClaim.ClaimValue && claim.Issuer == trustedIssuer));
    }

    private static ClaimsIdentity[] AssuranceIdentities(ClaimsPrincipal? principal, MfaClaimConfig config, string trustedIssuer)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedIssuer);
        if (principal == null || IsRestricted(principal))
            return [];

        var identities = principal.Identities.Where(identity => identity.IsAuthenticated).ToArray();
        var subject = identities.Length == 0 ? null : AssuranceSubject(identities[0]);
        if (subject == null || subject.Issuer != trustedIssuer ||
            identities.Any(identity => !SameSubject(subject, AssuranceSubject(identity))))
            return [];

        return identities.Where(identity => identity.Claims.Any(claim => claim.Type == config.ClaimType &&
            claim.Value == config.ClaimValue && claim.Issuer == trustedIssuer)).ToArray();
    }

    private static Claim? AssuranceSubject(ClaimsIdentity identity)
    {
        var subjects = identity.Claims.Where(claim => claim.Type == ClaimConventions.NameIdentifier || claim.Type == "sub").ToArray();
        var subject = subjects.FirstOrDefault();
        return subject != null && subjects.All(candidate => SameSubject(subject, candidate)) ? subject : null;
    }

    /// <summary>Multiple authenticated identities must identify the same subject and issuer.</summary>
    public static bool HasSingleAccount(ClaimsPrincipal principal, string? subjectClaimType = null)
    {
        var identities = principal.Identities.Where(identity => identity.IsAuthenticated).ToArray();
        if (identities.Length == 0)
            return false;

        if (identities.Length == 1)
            return true;

        var subject = GetSubject(identities[0], subjectClaimType);
        return subject != null && identities.Skip(1).All(identity => SameSubject(subject, GetSubject(identity, subjectClaimType)));
    }

    public static bool MatchesIdentity(ClaimsIdentity identity, ClaimsIdentity proof)
    {
        if (!identity.IsAuthenticated || !proof.IsAuthenticated)
            return false;
        if (ReferenceEquals(identity, proof))
            return true;

        // Callers must additionally bind authentication evidence to the selected scheme.
        return string.Equals(identity.AuthenticationType, proof.AuthenticationType, StringComparison.Ordinal) &&
               SameSubject(GetSubject(identity), GetSubject(proof));
    }

    private static Claim? GetSubject(ClaimsIdentity identity, string? subjectClaimType = null) =>
        identity.FindFirst(subjectClaimType ?? ClaimConventions.NameIdentifier) ??
        (subjectClaimType == null ? identity.FindFirst("sub") : null);

    private static bool SameSubject(Claim? left, Claim? right) =>
        left != null && right != null && !string.IsNullOrWhiteSpace(left.Value) &&
        string.Equals(left.Value, right.Value, StringComparison.Ordinal) &&
        string.Equals(left.Issuer, right.Issuer, StringComparison.Ordinal);
}
