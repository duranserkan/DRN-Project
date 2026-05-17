using System.Threading.RateLimiting;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.RateLimiting;

/// <summary>
/// Default post-auth rate limiting rule. Partitions by authenticated stable user id
/// (<c>NameIdentifier</c>/<c>sub</c>) with fallback to <c>RemoteIpAddress</c> for anonymous requests.
/// Reads token bucket parameters from <see cref="DrnAppFeatures"/>.
/// <para>
/// Auto-registered through <see cref="SingletonRateLimitRule"/> so it can compose with application-specific rules.
/// </para>
/// </summary>
public class DefaultPostAuthRateLimitRule(DrnAppFeatures features) : SingletonRateLimitRule
{
    private readonly Func<string, TokenBucketRateLimiterOptions> _optionsFactory =
        _ => RateLimitTokenBucketOptions.CreatePostAuth(features.RateLimit);

    /// <inheritdoc />
    public override int Order => int.MaxValue;

    /// <inheritdoc />
    public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        => RateLimitRuleResult.TokenBucket(GetPartitionKey(context), _optionsFactory);

    private static string GetPartitionKey(HttpContext context)
        => RateLimitPartitionKeys.GetPostAuthPartitionKey(context);
}
