namespace Sample.Domain.Identity;

public abstract class UserClaims
{
    public const string AccountId = nameof(AccountId);
    public const string TenantId = nameof(TenantId);
    public const string PPVersion = nameof(PPVersion);
    public const string SlimUI = nameof(SlimUI);
    public const string SwaggerAccess = nameof(SwaggerAccess);
    public const string TestPagesAccess = nameof(TestPagesAccess);
}
