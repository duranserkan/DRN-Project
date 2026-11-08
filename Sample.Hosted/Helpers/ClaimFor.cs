using System.Diagnostics.CodeAnalysis;
using Sample.Domain.Identity;

namespace Sample.Hosted.Helpers;

public class ClaimFor
{
    public ProfileClaimFor Profile { get; } = new();
    public TenantClaimFor Tenant { get; } = new();
    public AccountClaimFor Account { get; } = new();
}

[SuppressMessage("SonarQube", "S2325", Justification = "Get.Claim.Profile exposes these request-scoped claim values through an instance.")]
public class ProfileClaimFor
{
    public int PPVersion => ScopeContext.GetClaimParameter<int>(UserClaims.PPVersion);
    public bool SlimUi => ScopeContext.IsClaimFlagEnabled(UserClaims.SlimUI);
}

public class AccountClaimFor
{
    [SuppressMessage("SonarQube", "S2325", Justification = "Get.Claim.Account exposes the current account ID through this instance.")]
    public Guid? Id => ScopeContext.GetClaimParameter<Guid>(UserClaims.AccountId);
}

public class TenantClaimFor
{
    [SuppressMessage("SonarQube", "S2325", Justification = "Get.Claim.Tenant exposes the current tenant ID through this instance.")]
    public Guid? Id => ScopeContext.GetClaimParameter<Guid>(UserClaims.TenantId);
}
