using DRN.Framework.Utils.Scope;

namespace DRN.Framework.Utils.Auth.MFA;

public static class MFAFor
{
    public static bool MFAInProgress => ScopeContext.User.AuthenticationMethod == MFAClaimValues.MFAInProgress;
    public static bool MFASetupRequired => ScopeContext.User.AuthenticationMethod == MFAClaimValues.MFASetupRequired;

    //https://datatracker.ietf.org/doc/html/rfc8176#section-2
    //https://github.com/dotnet/aspnetcore/blob/b2c348b222ffd4f5f5a49ff90f5cd237d51e5231/src/Identity/Core/src/SignInManager.cs#L501
    public static bool MFACompleted => ScopeContext.User.Amr == MFAClaimValues.Amr;
    public static bool MFARenewalRequired =>
        ScopeContext.User.Authenticated &&
        ScopeContext.User.AuthenticationMethod == null &&
        !MFACompleted;
}