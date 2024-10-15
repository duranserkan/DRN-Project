using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Scope;
using Sample.Domain.Identity;

namespace Sample.Hosted.Auth.Claims;

public abstract class ClaimFor
{
    public static ProfileFor Profile { get; } = new();
    public static AuthFor Auth { get; } = new();
}

public class ProfileFor
{
    public int PPVersion => ScopeContext.GetClaimParameter<int>(UserClaims.PPVersion);
    public bool SlimUi => ScopeContext.IsClaimFlagEnabled(UserClaims.SlimUI);
}

public class AuthFor
{
    public bool MFAInProgress => ScopeContext.HasClaimValue(ClaimConventions.AuthenticationMethod, UserClaims.MFAInProgress);
    public bool MFASetupRequired => ScopeContext.HasClaimValue(ClaimConventions.AuthenticationMethod, UserClaims.MFASetupRequired);
}