using System.Threading.RateLimiting;

namespace DRN.Framework.Hosting.RateLimiting;

internal readonly struct RateLimitRulePartitionKey : IEquatable<RateLimitRulePartitionKey>
{
    private readonly RateLimitRulePhase _phase;
    private readonly RateLimitRulePartitionKeyKind _kind;
    private readonly Type? _ruleType;
    private readonly int _index;
    private readonly string? _partitionKey;
    private readonly string? _sourcePartitionKey;
    private readonly Func<string, RateLimiter>? _factory;

    private RateLimitRulePartitionKey(
        RateLimitRulePhase phase,
        RateLimitRulePartitionKeyKind kind,
        Type? ruleType = null,
        int index = 0,
        string? partitionKey = null,
        string? sourcePartitionKey = null,
        Func<string, RateLimiter>? factory = null)
    {
        _phase = phase;
        _kind = kind;
        _ruleType = ruleType;
        _index = index;
        _partitionKey = partitionKey;
        _sourcePartitionKey = sourcePartitionKey;
        _factory = factory;
    }

    internal static RateLimitRulePartitionKey Limit(
        RateLimitRulePhase phase,
        Type ruleType,
        RateLimitRuleResult result)
    {
        ArgumentNullException.ThrowIfNull(ruleType);
        ArgumentException.ThrowIfNullOrWhiteSpace(result.PartitionKey);
        ArgumentNullException.ThrowIfNull(result.Partition.Factory);

        return new RateLimitRulePartitionKey(
            phase,
            RateLimitRulePartitionKeyKind.Limit,
            ruleType,
            partitionKey: result.PartitionKey,
            sourcePartitionKey: result.Partition.PartitionKey,
            factory: result.Partition.Factory);
    }

    internal static RateLimitRulePartitionKey NoLimiter(
        RateLimitRulePhase phase,
        Type ruleType,
        RateLimitRulePartitionKeyKind reason) =>
        new(phase, reason, ruleType);

    internal static RateLimitRulePartitionKey NoLimiter(
        RateLimitRulePhase phase,
        RateLimitRulePartitionKeyKind reason,
        int index) =>
        new(phase, reason, index: index);

    internal RateLimiter CreateLimiter()
    {
        var factory = _factory ?? throw new InvalidOperationException("Rate limit partition factory is not configured.");
        return factory(_sourcePartitionKey!);
    }

    public bool Equals(RateLimitRulePartitionKey other) =>
        _phase == other._phase
        && _kind == other._kind
        && _ruleType == other._ruleType
        && _index == other._index
        && string.Equals(_partitionKey, other._partitionKey, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RateLimitRulePartitionKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_phase, _kind, _ruleType, _index, _partitionKey);
}

internal enum RateLimitRulePartitionKeyKind
{
    Limit,
    Skip,
    Stopped,
    Allow,
    ScopedStopped,
    ScopedMissing
}
