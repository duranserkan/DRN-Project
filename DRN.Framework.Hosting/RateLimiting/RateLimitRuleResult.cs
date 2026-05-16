using System.Threading.RateLimiting;

namespace DRN.Framework.Hosting.RateLimiting;

/// <summary>
/// Evaluation result returned by <see cref="IRateLimitRule.EvaluatePreAuth"/> or <see cref="IRateLimitRule.EvaluatePostAuth"/>.
/// Carries the selected limiter partition without capturing request state in cached limiter instances.
/// </summary>
public sealed class RateLimitRuleResult
{
    private RateLimitRuleResult()
    {
    }

    /// <summary>
    /// Partition key for rate limiter lease acquisition (for example IP address, user id, or tenant id).
    /// </summary>
    public required string PartitionKey { get; init; }

    /// <summary>
    /// Partition selected by the rule. The framework owns acquisition and rejection handling.
    /// </summary>
    public required RateLimitPartition<string> Partition { get; init; }

    /// <summary>
    /// When true, skip remaining rules after this rule is applied. Defaults to false so multiple
    /// matching rules can compose as a native chained limiter (for example tenant + user + IP).
    /// Auto-set when <see cref="Allow"/> is true.
    /// </summary>
    public bool StopRemainingRules { get; init; }

    /// <summary>
    /// When true, allow the request without rate limiting (whitelist).
    /// Implies <see cref="StopRemainingRules"/>.
    /// Use case: internal health checks, trusted IPs, service-to-service calls.
    /// </summary>
    public bool Allow { get; init; }

    internal bool ShouldStopRemainingRules => StopRemainingRules || Allow;

    /// <summary>
    /// Creates a result from a custom partition. Prefer the algorithm-specific helpers when possible.
    /// </summary>
    public static RateLimitRuleResult CustomPartition(
        string partitionKey,
        RateLimitPartition<string> partition,
        bool stopRemainingRules = false) =>
        Create(partitionKey, partition, stopRemainingRules);

    /// <summary>
    /// Allows the request without rate limiting.
    /// </summary>
    public static RateLimitRuleResult AllowRequest(string partitionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

        return new RateLimitRuleResult
        {
            PartitionKey = partitionKey,
            Partition = RateLimitPartition.GetNoLimiter(partitionKey),
            Allow = true
        };
    }

    /// <summary>
    /// Creates a token bucket result. This is the default choice for burst-friendly request limiting.
    /// </summary>
    public static RateLimitRuleResult TokenBucket(
        string partitionKey,
        Func<string, TokenBucketRateLimiterOptions> optionsFactory,
        bool stopRemainingRules = false)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);

        return Create(partitionKey,
            RateLimitPartition.GetTokenBucketLimiter(partitionKey, optionsFactory),
            stopRemainingRules);
    }

    /// <summary>
    /// Creates a fixed-window result.
    /// </summary>
    public static RateLimitRuleResult FixedWindow(
        string partitionKey,
        Func<string, FixedWindowRateLimiterOptions> optionsFactory,
        bool stopRemainingRules = false)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);

        return Create(partitionKey,
            RateLimitPartition.GetFixedWindowLimiter(partitionKey, optionsFactory),
            stopRemainingRules);
    }

    /// <summary>
    /// Creates a sliding-window result.
    /// </summary>
    public static RateLimitRuleResult SlidingWindow(
        string partitionKey,
        Func<string, SlidingWindowRateLimiterOptions> optionsFactory,
        bool stopRemainingRules = false)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);

        return Create(partitionKey,
            RateLimitPartition.GetSlidingWindowLimiter(partitionKey, optionsFactory),
            stopRemainingRules);
    }

    /// <summary>
    /// Creates a concurrency-limiter result.
    /// </summary>
    public static RateLimitRuleResult ConcurrencyLimiter(
        string partitionKey,
        Func<string, ConcurrencyLimiterOptions> optionsFactory,
        bool stopRemainingRules = false)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);

        return Create(partitionKey,
            RateLimitPartition.GetConcurrencyLimiter(partitionKey, optionsFactory),
            stopRemainingRules);
    }

    private static RateLimitRuleResult Create(
        string partitionKey,
        RateLimitPartition<string> partition,
        bool stopRemainingRules = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

        return new RateLimitRuleResult
        {
            PartitionKey = partitionKey,
            Partition = partition,
            StopRemainingRules = stopRemainingRules
        };
    }
}
