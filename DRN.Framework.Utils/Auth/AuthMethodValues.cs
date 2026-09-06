using System.Diagnostics.CodeAnalysis;

namespace DRN.Framework.Utils.Auth;

/// <summary>Registered amr values from RFC 8176. A method such as otp alone does not prove MFA.</summary>
public static class AuthMethodValues
{
    [SuppressMessage("SonarQube", "S2068", Justification = "RFC 8176 registered amr (Authentication Method Reference) value for password authentication ('pwd'), not a credential.")]
    public const string Password = "pwd";
    public const string OneTimePassword = "otp";
    public const string MultiFactor = "mfa";
}
