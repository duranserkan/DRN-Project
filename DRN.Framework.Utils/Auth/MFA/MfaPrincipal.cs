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

    public static bool IsCompleted(ClaimsPrincipal? principal, AuthenticationClaimConfig config) =>
        principal != null && !IsRestricted(principal) && HasSingleAccount(principal, config) &&
        principal.Identities.Any(identity => identity.IsAuthenticated && identity.Claims.Any(claim =>
            claim.Type == config.Mfa.ClaimType && claim.Value == config.Mfa.ClaimValue));

    /// <summary>Checks completed MFA and recent authentication evidence from an explicitly trusted issuer.</summary>
    /// <remarks>
    /// The completed marker and timestamp must belong to the same authenticated identity and issuer.
    /// The age boundary is inclusive; future, missing, malformed and conflicting timestamps fail closed.
    /// auth_time describes authentication, not necessarily the last MFA verification. For recent MFA,
    /// select a timestamp whose verified-MFA meaning is guaranteed by the issuing provider.
    /// This helper does not change the default Mfa policy or issue evidence.
    /// </remarks>
    public static bool IsRecent(ClaimsPrincipal? principal, AuthenticationClaimConfig config, string trustedIssuer,
        TimeSpan maximumAge, DateTimeOffset utcNow, string authenticationTimeClaimType = AuthClaimTypes.AuthenticationTime)
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
    public static bool IsPhishingResistant(ClaimsPrincipal? principal, AuthenticationClaimConfig config, string trustedIssuer,
        MfaClaimConfig assuranceClaim)
    {
        ArgumentNullException.ThrowIfNull(assuranceClaim);
        var identities = AssuranceIdentities(principal, config, trustedIssuer);
        if (config.Mfa == assuranceClaim)
            return false;

        return identities.Any(identity => identity.Claims.Any(claim => claim.Type == assuranceClaim.ClaimType &&
            claim.Value == assuranceClaim.ClaimValue && claim.Issuer == trustedIssuer));
    }

    private static ClaimsIdentity[] AssuranceIdentities(ClaimsPrincipal? principal, AuthenticationClaimConfig config, string trustedIssuer)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedIssuer);
        if (principal == null || IsRestricted(principal))
            return [];

        var identities = principal.Identities.Where(identity => identity.IsAuthenticated).ToArray();
        var subject = identities.Length == 0 ? null : SubjectClaims.Find(identities[0], config);
        if (subject == null || subject.Issuer != trustedIssuer ||
            identities.Skip(1).Any(identity => !SameSubject(subject, SubjectClaims.Find(identity, config))))
            return [];

        return identities.Where(identity => identity.Claims.Any(claim => claim.Type == config.Mfa.ClaimType &&
            claim.Value == config.Mfa.ClaimValue && claim.Issuer == trustedIssuer)).ToArray();
    }

    /// <summary>Multiple authenticated identities must identify the same subject and issuer.</summary>
    public static bool HasSingleAccount(ClaimsPrincipal principal, AuthenticationClaimConfig? config = null, bool requireSubject = false)
    {
        config ??= AuthenticationClaimConfig.Default;
        ClaimsIdentity? first = null;
        Claim? subject = null;
        foreach (var identity in principal.Identities)
        {
            if (!identity.IsAuthenticated)
                continue;
            if (first == null)
            {
                first = identity;
                subject = SubjectClaims.Find(identity, config);
                // Subjectless single identities remain valid for provider-neutral completion.
                // An explicit mapping, or supplied standard aliases, must be unambiguous.
                if ((requireSubject || !config.HasDefaultSubjectMapping || identity.HasClaim(claim => SubjectClaims.IsDefaultSubjectType(claim.Type))) &&
                    subject == null)
                    return false;
                continue;
            }

            if (!SameSubject(subject, SubjectClaims.Find(identity, config)))
                return false;
        }

        return first != null;
    }

    public static bool MatchesIdentity(ClaimsIdentity identity, ClaimsIdentity proof, AuthenticationClaimConfig? config = null)
    {
        config ??= AuthenticationClaimConfig.Default;
        if (!identity.IsAuthenticated || !proof.IsAuthenticated)
            return false;
        if (ReferenceEquals(identity, proof) && config.HasDefaultSubjectMapping &&
            !identity.HasClaim(claim => SubjectClaims.IsDefaultSubjectType(claim.Type)))
            return true;

        // Callers must additionally bind authentication evidence to the selected scheme.
        return string.Equals(identity.AuthenticationType, proof.AuthenticationType, StringComparison.Ordinal) &&
               SameSubject(SubjectClaims.Find(identity, config), SubjectClaims.Find(proof, config));
    }

    private static bool SameSubject(Claim? left, Claim? right) =>
        left != null && right != null && !string.IsNullOrWhiteSpace(left.Value) &&
        string.Equals(left.Value, right.Value, StringComparison.Ordinal) &&
        string.Equals(left.Issuer, right.Issuer, StringComparison.Ordinal);
}
