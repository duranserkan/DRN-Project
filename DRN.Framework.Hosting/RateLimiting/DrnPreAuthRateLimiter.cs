using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.RateLimiting;

/// <summary>
/// Dedicated wrapper for DRN's pre-auth limiter so unrelated application registrations of
/// <see cref="PartitionedRateLimiter{TResource}"/> cannot replace early abuse protection.
/// </summary>
public sealed class DrnPreAuthRateLimiter(PartitionedRateLimiter<HttpContext> limiter) : IDisposable, IAsyncDisposable
{
    public ValueTask<RateLimitLease> AcquireAsync(HttpContext context, CancellationToken cancellationToken = default) =>
        limiter.AcquireAsync(context, cancellationToken: cancellationToken);

    public void Dispose()
    {
        if (limiter is IDisposable disposable)
            disposable.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        if (limiter is IAsyncDisposable asyncDisposable)
            return asyncDisposable.DisposeAsync();

        Dispose();
        return ValueTask.CompletedTask;
    }
}
