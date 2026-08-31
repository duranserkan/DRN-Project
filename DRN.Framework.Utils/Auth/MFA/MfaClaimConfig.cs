using DRN.Framework.Utils.Auth;

namespace DRN.Framework.Utils.Auth.MFA;

/// <summary>
/// Identifies the claim that proves a completed multi-factor authentication session.
/// </summary>
public sealed record MfaClaimConfig
{
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
