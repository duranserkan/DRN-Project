using System.Threading.RateLimiting;
using DRN.Framework.Hosting.RateLimiting;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.DrnProgram.Configurators;

internal static class DrnRateLimitConfigurator
{
    internal static PartitionedRateLimiter<HttpContext> CreatePreAuthRateLimiter(IServiceProvider serviceProvider) =>
        RateLimitRuleChainFactory.Create(serviceProvider.GetRequiredService<RateLimitRuleRegistry>(), RateLimitRulePhase.PreAuth,
            static (rule, context) => rule.EvaluatePreAuth(context), includeScopedRules: false);

    internal static void ConfigurePostAuthRateLimiterOptions(RateLimiterOptions options, IServiceProvider serviceProvider)
    {
        var configuredOnRejected = options.OnRejected;
        // ASP.NET Core defaults rate-limiter rejection to 503; DRN's security-first default is 429.
        // Apps can set 503 after calling the base ConfigurePostAuthRateLimiterOptions hook.
        options.RejectionStatusCode = options.RejectionStatusCode == StatusCodes.Status503ServiceUnavailable
            ? StatusCodes.Status429TooManyRequests
            : options.RejectionStatusCode;
        options.GlobalLimiter = RateLimitRuleChainFactory.Create(
            serviceProvider.GetRequiredService<RateLimitRuleRegistry>(),
            RateLimitRulePhase.PostAuth,
            static (rule, context) => rule.EvaluatePostAuth(context));

        options.OnRejected = async (context, cancellationToken) =>
        {
            if (!context.HttpContext.Response.HasStarted)
                context.HttpContext.Response.StatusCode = options.RejectionStatusCode;

            if (!context.HttpContext.Response.HasStarted && context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var seconds = (int)Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds));
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString();
            }

            var scopedLog = context.HttpContext.RequestServices.GetRequiredService<IScopedLog>();
            var telemetry = context.HttpContext.RequestServices.GetRequiredService<RateLimitTelemetry>();
            var features = context.HttpContext.RequestServices.GetRequiredService<DrnAppFeatures>();
            var securitySettings = context.HttpContext.RequestServices.GetRequiredService<IAppSecuritySettings>();
            var rejectedMatch = context.HttpContext.GetRejectedRateLimitRuleMatch();
            var partitionKey = rejectedMatch?.Result.PartitionKey ?? RateLimitPartitionKeys.GetPostAuthPartitionKey(context.HttpContext);
            telemetry.RecordRejection(context.HttpContext, RateLimitRulePhase.PostAuth, rejectedMatch);
            scopedLog.Add("PostAuthRateLimitRejected", true);
            scopedLog.Add("PostAuthRateLimitRejectedRule", rejectedMatch?.Rule.GetType().FullName ?? string.Empty);
            scopedLog.Add("PostAuthRateLimitRejectedPartition", RateLimitPartitionRedactor.Format(partitionKey, features.RateLimit, securitySettings));

            var matchedRule = rejectedMatch?.Rule;
            if (matchedRule != null)
                await matchedRule.OnRejectedAsync(context.HttpContext, context.Lease, cancellationToken);

            if (configuredOnRejected != null)
                await configuredOnRejected(context, cancellationToken);
        };
    }
}
