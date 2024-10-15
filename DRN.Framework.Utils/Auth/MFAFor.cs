using DRN.Framework.Utils.Scope;

namespace DRN.Framework.Utils.Auth;

public static class MFAFor
{
    public static bool MFAInProgress => ScopeContext.HasClaimValue(ClaimConventions.AuthenticationMethod, MFAClaims.MFAInProgress);
    public static bool MFASetupRequired => ScopeContext.HasClaimValue(ClaimConventions.AuthenticationMethod, MFAClaims.MFASetupRequired);
}