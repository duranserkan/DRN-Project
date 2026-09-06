namespace DRN.Framework.Utils.Auth;

/// <summary>Standard OIDC claim names and DRN's role convention; use exact claim-type spelling.</summary>
public static class AuthClaimTypes
{
    public const string Subject = "sub";
    public const string Issuer = "iss";
    public const string Name = "name";
    public const string Email = "email";
    public const string EmailVerified = "email_verified";
    public const string AuthenticationMethods = "amr";
    public const string AuthenticationContext = "acr";
    public const string AuthenticationTime = "auth_time";

    /// <summary>DRN's flat, repeated role claim; role values remain application-owned.</summary>
    public const string Roles = "roles";
}
