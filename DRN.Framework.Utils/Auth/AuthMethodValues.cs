namespace DRN.Framework.Utils.Auth;

/// <summary>Registered amr values from RFC 8176. A method such as otp alone does not prove MFA.</summary>
public static class AuthMethodValues
{
    public const string Password = "pwd";
    public const string OneTimePassword = "otp";
    public const string MultiFactor = "mfa";
}
