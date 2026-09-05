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
