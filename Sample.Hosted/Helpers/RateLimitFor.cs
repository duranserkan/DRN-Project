namespace Sample.Hosted.Helpers;

/// <summary>
/// Sample-hosted rate-limit partition helpers. Applications should keep domain-specific
/// claim names here instead of adding them to DRN.Framework.Hosting.
/// </summary>
public class RateLimitFor
{
    public string? AccountPartition => Get.Claim.Account.Id == null ? null : $"account:{Get.Claim.Account.Id:N}";
    public string? TenantPartition => Get.Claim.Tenant.Id == null ? null : $"tenant:{Get.Claim.Tenant.Id:N}";
}
