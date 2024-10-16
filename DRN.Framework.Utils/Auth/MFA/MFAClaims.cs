namespace DRN.Framework.Utils.Auth.MFA;

public static class MFAClaims
{
    /// <summary>
    /// AuthenticationMethodReference
    /// </summary>
    public const string AuthenticationMethodValue = "mfa";
    public const string MFAInProgress = nameof(MFAInProgress);
    public const string MFASetupRequired = nameof(MFASetupRequired);
}