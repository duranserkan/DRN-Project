using Sample.Domain.Identity;

namespace Sample.Hosted.Helpers;

public class ClaimFor
{
    public ProfileClaimFor Profile { get; } = new();
    public TenantClaimFor Tenant { get; } = new();
    public AccountClaimFor Account { get; } = new();
}

public class ProfileClaimFor
{
    public int PPVersion => ScopeContext.GetClaimParameter<int>(UserClaims.PPVersion);
    public bool SlimUi => ScopeContext.IsClaimFlagEnabled(UserClaims.SlimUI);
}

public class AccountClaimFor
{
    public Guid? Id => ScopeContext.GetClaimParameter<Guid>(UserClaims.AccountId);
}

public class TenantClaimFor
{
    public Guid? Id => ScopeContext.GetClaimParameter<Guid>(UserClaims.TenantId);
}
