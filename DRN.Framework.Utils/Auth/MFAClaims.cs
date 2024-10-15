namespace DRN.Framework.Utils.Auth;

public static class MFAClaims
{
    /// <summary>
    /// AuthenticationMethodReference
    /// </summary>
    public const string Amr = "mfa";
    public const string MFAInProgress = nameof(MFAInProgress);
    public const string MFASetupRequired = nameof(MFASetupRequired);
}