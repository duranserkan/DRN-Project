namespace DRN.Framework.Utils.Auth.MFA;

public static class MfaClaimValues
{
    //https://datatracker.ietf.org/doc/html/rfc8176#section-2
    //https://github.com/dotnet/aspnetcore/blob/b2c348b222ffd4f5f5a49ff90f5cd237d51e5231/src/Identity/Core/src/SignInManager.cs#L501
    public const string Amr = "mfa";
    public const string MfaInProgress = nameof(MfaInProgress);
    public const string MfaSetupRequired = nameof(MfaSetupRequired);
}