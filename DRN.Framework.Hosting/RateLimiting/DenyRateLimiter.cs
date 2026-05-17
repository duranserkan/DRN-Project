using System.Threading.RateLimiting;

namespace DRN.Framework.Hosting.RateLimiting;

internal sealed class DenyRateLimiter(TimeSpan? retryAfter) : RateLimiter
{
    private readonly RateLimitLease _failedLease = new DenyRateLimitLease(retryAfter);
    private long _totalFailedLeases;

    public override TimeSpan? IdleDuration => TimeSpan.Zero;

    public override RateLimiterStatistics GetStatistics() => new()
    {
        CurrentAvailablePermits = 0,
        CurrentQueuedCount = 0,
        TotalFailedLeases = Interlocked.Read(ref _totalFailedLeases),
        TotalSuccessfulLeases = 0
    };

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        Interlocked.Increment(ref _totalFailedLeases);
        return _failedLease;
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _totalFailedLeases);
        return ValueTask.FromResult(_failedLease);
    }

    private sealed class DenyRateLimitLease(TimeSpan? retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames =>
            retryAfter.HasValue ? [MetadataName.RetryAfter.Name] : [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (retryAfter.HasValue && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = retryAfter.Value;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
