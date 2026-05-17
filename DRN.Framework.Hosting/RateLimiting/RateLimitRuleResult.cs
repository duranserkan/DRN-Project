using System.Threading.RateLimiting;

namespace DRN.Framework.Hosting.RateLimiting;

/// <summary>
/// Action selected by a matching rate limit rule result.
/// </summary>
public enum RateLimitRuleAction
{
    /// <summary>
    /// Acquire the selected limiter partition.
    /// </summary>
    Limit,

    /// <summary>
    /// Allow the request without acquiring a limiter.
    /// </summary>
    Allow,

    /// <summary>
    /// Reject the request immediately.
    /// </summary>
    Deny
}

/// <summary>
/// Evaluation result returned by <see cref="IRateLimitRule.EvaluatePreAuth"/> or <see cref="IRateLimitRule.EvaluatePostAuth"/>.
/// Carries the selected limiter partition without capturing request state in cached limiter instances.
/// </summary>
public readonly record struct RateLimitRuleResult
{
    private RateLimitRuleResult(
        string partitionKey,
        RateLimitPartition<string> partition,
        RateLimitRuleAction action = RateLimitRuleAction.Limit,
        bool stopRemainingRules = false)
    {
        PartitionKey = partitionKey;
        Partition = partition;
        Action = action;
        StopRemainingRules = stopRemainingRules;
    }

    /// <summary>
    /// Partition key for rate limiter lease acquisition (for example IP address, user id, or tenant id).
    /// </summary>
    public string PartitionKey { get; } = string.Empty;

    /// <summary>
    /// Partition selected by the rule. The framework owns acquisition and rejection handling.
    /// </summary>
    public RateLimitPartition<string> Partition { get; }

    /// <summary>
    /// Selected action for this result. Quota helpers use <see cref="RateLimitRuleAction.Limit"/>,
    /// <see cref="AllowRequest"/> uses <see cref="RateLimitRuleAction.Allow"/>, and
    /// <see cref="DenyRequest"/> uses <see cref="RateLimitRuleAction.Deny"/>.
    /// </summary>
    public RateLimitRuleAction Action { get; } = RateLimitRuleAction.Limit;

    /// <summary>
    /// When true, skip remaining rules after this result is selected. Defaults to false so
    /// quota results can compose as a native chained limiter (for example tenant + user + IP).
    /// <see cref="AllowRequest"/> and <see cref="DenyRequest"/> set this to true.
    /// </summary>
    public bool StopRemainingRules { get; }

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

        return new RateLimitRuleResult(
            partitionKey,
            RateLimitPartition.GetNoLimiter(partitionKey),
            RateLimitRuleAction.Allow,
            stopRemainingRules: true);
    }

    /// <summary>
    /// Rejects the request immediately without evaluating remaining rules.
    /// </summary>
    public static RateLimitRuleResult DenyRequest(string partitionKey, TimeSpan? retryAfter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

        return new RateLimitRuleResult(
            partitionKey,
            RateLimitPartition.Get(partitionKey, _ => new DenyRateLimiter(retryAfter)),
            RateLimitRuleAction.Deny,
            stopRemainingRules: true);
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
        ArgumentNullException.ThrowIfNull(partition.Factory);

        return new RateLimitRuleResult(partitionKey, partition, stopRemainingRules: stopRemainingRules);
    }
}
