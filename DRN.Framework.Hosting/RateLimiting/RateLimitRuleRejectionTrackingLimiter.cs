using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.RateLimiting;

internal sealed class RateLimitRuleRejectionTrackingLimiter(PartitionedRateLimiter<HttpContext> inner)
    : PartitionedRateLimiter<HttpContext>
{
    public override RateLimiterStatistics? GetStatistics(HttpContext resource) => inner.GetStatistics(resource);

    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount)
    {
        var lease = inner.AttemptAcquire(resource, permitCount);
        TrackRejected(resource, lease);

        return lease;
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        HttpContext resource,
        int permitCount,
        CancellationToken cancellationToken)
    {
        var leaseTask = inner.AcquireAsync(resource, permitCount, cancellationToken);
        if (leaseTask.IsCompletedSuccessfully)
        {
            var lease = leaseTask.Result;
            TrackRejected(resource, lease);

            return ValueTask.FromResult(lease);
        }

        return AwaitAndTrackRejectedAsync(resource, leaseTask);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
    }

    protected override ValueTask DisposeAsyncCore() => inner.DisposeAsync();

    private static async ValueTask<RateLimitLease> AwaitAndTrackRejectedAsync(
        HttpContext resource,
        ValueTask<RateLimitLease> leaseTask)
    {
        var lease = await leaseTask.ConfigureAwait(false);
        TrackRejected(resource, lease);

        return lease;
    }

    private static void TrackRejected(HttpContext resource, RateLimitLease lease)
    {
        if (!lease.IsAcquired)
            resource.SetRejectedRateLimitRuleMatch();
    }
}
