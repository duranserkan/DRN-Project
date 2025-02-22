using DRN.Framework.Utils.Scope;
using Sample.Domain.Identity;

namespace Sample.Hosted.Utils;
//Todo: unify all for values under single parent For object
public abstract class ClaimFor
{
    public static ProfileClaimFor ProfileClaim { get; } = new();
}

public class ProfileClaimFor
{
    public int PPVersion => ScopeContext.GetClaimParameter<int>(UserClaims.PPVersion);
    public bool SlimUi => ScopeContext.IsClaimFlagEnabled(UserClaims.SlimUI);
}