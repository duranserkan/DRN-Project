using DRN.Framework.Utils.Scope;

namespace DRN.Framework.Utils.Auth.MFA;

public static class MFAFor
{
    public static bool MFAInProgress => ScopeContext.HasClaimValue(ClaimConventions.AuthenticationMethod, MFAClaims.MFAInProgress);
    public static bool MFASetupRequired => ScopeContext.HasClaimValue(ClaimConventions.AuthenticationMethod, MFAClaims.MFASetupRequired);
    public static bool MFACompleted => ScopeContext.HasClaimValue(ClaimConventions.AuthenticationMethod, MFAClaims.AuthenticationMethodValue);
}