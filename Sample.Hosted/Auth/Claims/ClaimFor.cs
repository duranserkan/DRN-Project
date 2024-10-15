using DRN.Framework.Utils.Scope;
using Sample.Domain.Identity;

namespace Sample.Hosted.Auth.Claims;

public abstract class ClaimFor
{
    public static ProfileFor Profile { get; } = new();
}

public class ProfileFor
{
    public int PPVersion => ScopeContext.GetClaimParameter<int>(UserClaims.PPVersion);
    public bool SlimUi => ScopeContext.IsClaimFlagEnabled(UserClaims.SlimUI);
}