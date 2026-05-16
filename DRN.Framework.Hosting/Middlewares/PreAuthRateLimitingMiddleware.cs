using System.Diagnostics;
using System.Threading.RateLimiting;
using DRN.Framework.Hosting.RateLimiting;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares;

/// <summary>
/// Pre-authentication rate limiting middleware. Placed after <c>UseRouting()</c> and before
/// <c>UseAuthentication()</c> to reject abusive requests before any auth/MFA/authZ processing.
/// <para>
/// Acquires a lease from the pre-auth <paramref name="limiter"/>. Rule evaluation happens inside the
/// partition selector; the default pre-auth chain evaluates singleton rules only.
/// </para>
/// <para>
/// Returns 429 Too Many Requests with Retry-After header on rejection.
/// Emits <see cref="IScopedLog"/> warning when <c>RemoteIpAddress</c> is null
/// (detects <c>ForwardedHeaders</c> misconfiguration).
/// </para>
/// </summary>
public class PreAuthRateLimitingMiddleware(
    RequestDelegate next,
    DrnPreAuthRateLimiter limiter,
    RateLimitTelemetry telemetry)
{
    public async Task InvokeAsync(HttpContext context, IScopedLog scopedLog)
    {
        if (RateLimitEndpointMetadata.IsRateLimitingDisabled(context))
        {
            await next(context);
            return;
        }

        // Observability: warn on null IP (detects ForwardedHeaders misconfiguration)
        if (context.Connection.RemoteIpAddress == null)
            scopedLog.Add("PreAuthRateLimitNullIpWarning", "RemoteIpAddress is null, check ForwardedHeaders configuration");

        // The default chained limiter clears first; this guard is belt-and-suspenders
        // in case the limiter is replaced by an override that skips DRN rule selection.
        context.ClearRateLimitRuleSelection();
        var leaseStart = Stopwatch.GetTimestamp();
        using var lease = await limiter.AcquireAsync(context, context.RequestAborted);
        var match = context.GetRateLimitRuleMatch();

        if (lease.IsAcquired)
        {
            telemetry.RecordRequest(context, RateLimitRulePhase.PreAuth, "acquired", match);
            telemetry.AddActiveRequestLease(context, RateLimitRulePhase.PreAuth, 1, match);
            try
            {
                await next(context);
            }
            finally
            {
                telemetry.RecordRequestLeaseDuration(context, RateLimitRulePhase.PreAuth, Stopwatch.GetElapsedTime(leaseStart), match);
                telemetry.AddActiveRequestLease(context, RateLimitRulePhase.PreAuth, -1, match);
            }

            return;
        }

        // Rejection
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        telemetry.RecordRequest(context, RateLimitRulePhase.PreAuth, "rejected", match);
        telemetry.RecordRejection(context, RateLimitRulePhase.PreAuth, match);
        scopedLog.Add("PreAuthRateLimitRejected", true);
        scopedLog.Add("PreAuthRateLimitRejectedIp", ipAddress);
        scopedLog.Add("PreAuthRateLimitRejectedRule", match?.Rule.GetType().FullName ?? string.Empty);
        scopedLog.Add("PreAuthRateLimitRejectedPartition", match?.Result.PartitionKey ?? ipAddress);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = (int)Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds));
            context.Response.Headers.RetryAfter = seconds.ToString();
        }

        var matchedRule = match?.Rule;
        if (matchedRule != null)
            await matchedRule.OnRejectedAsync(context, lease, context.RequestAborted);
    }
}
