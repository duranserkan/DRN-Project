using DRN.Framework.Utils.Scope;
using Sample.Domain.Identity;

namespace Sample.Hosted.Auth.Claims;

public static class ClaimContext
{
    public static int PPVersion => ScopeContext.GetClaimParameter<int>(UserClaims.PPVersion);
    public static bool SlimUi => ScopeContext.IsClaimFlagEnabled(UserClaims.SlimUI);
    public static bool MFAInProgress => ScopeContext.Data.IsFlagEnabled(UserClaims.MFAInProgress);
}