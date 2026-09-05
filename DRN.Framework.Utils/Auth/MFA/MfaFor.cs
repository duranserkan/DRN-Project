using DRN.Framework.Utils.Scope;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.Auth.MFA;

public static class MfaFor
{
    public static bool MfaInProgress => ScopeContext.User.AuthenticationMethod == MfaClaimValues.MfaInProgress;
    public static bool MfaSetupRequired => ScopeContext.User.AuthenticationMethod == MfaClaimValues.MfaSetupRequired;

    public static bool MfaCompleted
    {
        get
        {
            var claims = ScopeContext.Services.GetService<AuthenticationClaimConfig>() ?? AuthenticationClaimConfig.Default;
            return MfaPrincipal.IsCompleted(ScopeContext.User.Principal, claims);
        }
    }
    public static bool MfaRenewalRequired =>
        ScopeContext.User.Authenticated &&
        !ScopeContext.User.HasExemptionScheme &&
        ScopeContext.User.AuthenticationMethod == null &&
        !MfaCompleted;
}
