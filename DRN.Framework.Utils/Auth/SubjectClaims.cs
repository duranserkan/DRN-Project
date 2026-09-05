using System.Security.Claims;

namespace DRN.Framework.Utils.Auth;

/// <summary>Resolves account evidence without accepting conflicting subject aliases.</summary>
public static class SubjectClaims
{
    public static Claim? Find(ClaimsIdentity identity, AuthenticationClaimConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var mapping = (config ?? AuthenticationClaimConfig.Default).Subject;

        // Reject case-variant collisions too: downstream .NET Identity lookups may ignore casing.
        var subjects = identity.Claims.Where(claim => mapping.MatchesIgnoringCase(claim.Type)).ToArray();
        var subject = subjects.FirstOrDefault(claim => claim.Type == mapping.Type) ??
                      subjects.FirstOrDefault(claim => mapping.Accepts(claim.Type));
        return subject != null && !string.IsNullOrWhiteSpace(subject.Value) &&
               subjects.All(claim => claim.Value == subject.Value && claim.Issuer == subject.Issuer)
            ? subject
            : null;
    }

    internal static bool IsDefaultSubjectType(string type) =>
        string.Equals(type, ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, AuthClaimTypes.Subject, StringComparison.OrdinalIgnoreCase);
}
