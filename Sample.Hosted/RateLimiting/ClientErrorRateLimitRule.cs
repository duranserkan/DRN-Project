using System.Threading.RateLimiting;
using DRN.Framework.Hosting.RateLimiting;
using DRN.Framework.Utils.Auth;
using Microsoft.AspNetCore.Mvc.Controllers;
using Sample.Hosted.Controllers.Sample;

namespace Sample.Hosted.RateLimiting;

public sealed class ClientErrorRateLimitRule(IScopedUser user) : ScopedRateLimitRule
{
    public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
    {
        var controller = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (controller?.ControllerTypeInfo.AsType() != typeof(ClientErrorController) ||
            !user.Authenticated || string.IsNullOrWhiteSpace(user.Id))
            return null;

        return RateLimitRuleResult.SlidingWindow(user.Id, static _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }
}
