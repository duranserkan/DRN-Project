namespace DRN.Framework.Utils.Auth.MFA;

/// <summary>
/// Identifies the claim that proves a completed multifactor authentication session.
/// </summary>
public sealed record MfaClaimConfig
{
    // TODO(MFA-07): Add trusted issuer-specific mappings for validated acr, amr, and authentication
    // timestamps, with OIDC interoperability tests for assurance, freshness, forwarding, and account isolation.
    public MfaClaimConfig(string claimType, string claimValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimValue);

        ClaimType = claimType;
        ClaimValue = claimValue;
    }

    /// <summary>
    /// ASP.NET Core Identity's default MFA marker: <c>amr=mfa</c>.
    /// </summary>
    public static MfaClaimConfig AspNetIdentity { get; } = new(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr);

    public string ClaimType { get; }
    public string ClaimValue { get; }
}
