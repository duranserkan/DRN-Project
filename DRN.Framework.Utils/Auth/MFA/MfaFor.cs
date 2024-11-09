using DRN.Framework.Utils.Scope;

namespace DRN.Framework.Utils.Auth.MFA;

public static class MfaFor
{
    public static bool MfaInProgress => ScopeContext.User.AuthenticationMethod == MfaClaimValues.MfaInProgress;
    public static bool MfaSetupRequired => ScopeContext.User.AuthenticationMethod == MfaClaimValues.MfaSetupRequired;

    //https://datatracker.ietf.org/doc/html/rfc8176#section-2
    //https://github.com/dotnet/aspnetcore/blob/b2c348b222ffd4f5f5a49ff90f5cd237d51e5231/src/Identity/Core/src/SignInManager.cs#L501
    public static bool MfaCompleted => ScopeContext.User.Amr == MfaClaimValues.Amr;
    public static bool MfaRenewalRequired =>
        ScopeContext.User.Authenticated &&
        ScopeContext.User.AuthenticationMethod == null &&
        !MfaCompleted;
}