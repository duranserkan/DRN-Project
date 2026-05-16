using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.RateLimiting;

/// <summary>
/// Default pre-auth rate limiting rule. Partitions by <see cref="HttpContext.Connection"/>
/// <c>RemoteIpAddress</c>. Reads token bucket parameters from <see cref="DrnAppFeatures"/>.
/// <para>
/// Auto-registered through <see cref="SingletonRateLimitRule"/> so it can compose with application-specific rules.
/// </para>
/// </summary>
public class DefaultPreAuthRateLimitRule(DrnAppFeatures features) : SingletonRateLimitRule
{
    /// <inheritdoc />
    public override int Order => int.MaxValue;

    /// <inheritdoc />
    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context) =>
        RateLimitRuleResult.TokenBucket(GetPartitionKey(context), _ => RateLimitTokenBucketOptions.CreatePreAuth(features.RateLimit));

    private static string GetPartitionKey(HttpContext context) =>
        RateLimitPartitionKeys.GetPreAuthPartitionKey(context);
}
