using System.Diagnostics;
using System.Diagnostics.Metrics;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.RateLimiting;

/// <summary>
/// Emits DRN rate limiting metrics through the .NET metrics pipeline.
/// </summary>
[Singleton<RateLimitTelemetry>]
public sealed class RateLimitTelemetry
{
    public const string MeterName = "DRN.Framework.Hosting.RateLimiting";

    private static readonly Meter FallbackMeter = new(MeterName);
    private readonly UpDownCounter<long> _activeRequestLeases;
    private readonly Histogram<double> _requestLeaseDuration;
    private readonly Counter<long> _requests;
    private readonly Counter<long> _rejections;

    public RateLimitTelemetry(IServiceProvider serviceProvider)
    {
        var meter = serviceProvider.GetService<IMeterFactory>()?.Create(MeterName) ?? FallbackMeter;
        _requests = meter.CreateCounter<long>(
            "drn.rate_limiting.requests",
            unit: "{request}",
            description: "Number of DRN rate limiting lease acquisition attempts.");
        _rejections = meter.CreateCounter<long>(
            "drn.rate_limiting.rejections",
            unit: "{request}",
            description: "Number of requests rejected by DRN rate limiting rules.");
        _activeRequestLeases = meter.CreateUpDownCounter<long>(
            "drn.rate_limiting.active_request_leases",
            unit: "{request}",
            description: "Number of active request leases acquired by DRN rate limiting.");
        _requestLeaseDuration = meter.CreateHistogram<double>(
            "drn.rate_limiting.request_lease.duration",
            unit: "s",
            description: "Duration of DRN rate limiting request leases.");
    }

    internal void RecordRequest(HttpContext context, RateLimitRulePhase phase, string result, RateLimitRuleMatch? match) =>
        _requests.Add(1, CreateTags(context, phase, result, match));

    internal void RecordRejection(HttpContext context, RateLimitRulePhase phase, RateLimitRuleMatch? match) =>
        _rejections.Add(1, CreateTags(context, phase, "rejected", match));

    internal void AddActiveRequestLease(HttpContext context, RateLimitRulePhase phase, long value, RateLimitRuleMatch? match) =>
        _activeRequestLeases.Add(value, CreateTags(context, phase, "acquired", match));

    internal void RecordRequestLeaseDuration(HttpContext context, RateLimitRulePhase phase, TimeSpan duration, RateLimitRuleMatch? match) =>
        _requestLeaseDuration.Record(duration.TotalSeconds, CreateTags(context, phase, "acquired", match));

    internal static TagList CreateTags(HttpContext context, RateLimitRulePhase phase, string result, RateLimitRuleMatch? match)
    {
        var tags = new TagList
        {
            { "drn.rate_limiting.phase", phase.ToString() },
            { "aspnetcore.rate_limiting.policy", RateLimitEndpointMetadata.GetPolicyName(context) },
            { "aspnetcore.rate_limiting.result", result },
            { "drn.rate_limiting.action", GetActionTag(match) }
        };

        var ruleType = match?.Rule.GetType().FullName;
        if (!string.IsNullOrWhiteSpace(ruleType))
            tags.Add("drn.rate_limiting.rule", ruleType);

        return tags;
    }

    private static string GetActionTag(RateLimitRuleMatch? match) =>
        match?.Result.Action switch
        {
            RateLimitRuleAction.Allow => "allow",
            RateLimitRuleAction.Deny => "deny",
            RateLimitRuleAction.Limit => "limit",
            _ => "unknown"
        };
}
