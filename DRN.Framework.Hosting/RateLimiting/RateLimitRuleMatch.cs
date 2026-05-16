using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.RateLimiting;

internal sealed record RateLimitRuleMatch(IRateLimitRule Rule, RateLimitRuleResult Result);

internal static class RateLimitRuleHttpContextItems
{
    private const string MatchedRuleKey = "__DRN_RateLimitRuleMatch";
    private const string StopRemainingRulesKey = "__DRN_RateLimitStopRemainingRules";

    internal static void SetRateLimitRuleMatch(this HttpContext context, IRateLimitRule rule, RateLimitRuleResult result) =>
        context.Items[MatchedRuleKey] = new RateLimitRuleMatch(rule, result);

    internal static RateLimitRuleMatch? GetRateLimitRuleMatch(this HttpContext context) =>
        context.Items.TryGetValue(MatchedRuleKey, out var value) ? value as RateLimitRuleMatch : null;

    internal static void StopRemainingRateLimitRules(this HttpContext context) =>
        context.Items[StopRemainingRulesKey] = true;

    internal static bool ShouldStopRemainingRateLimitRules(this HttpContext context) =>
        context.Items.TryGetValue(StopRemainingRulesKey, out var value) && value is true;

    internal static void ClearRateLimitRuleSelection(this HttpContext context)
    {
        context.Items.Remove(MatchedRuleKey);
        context.Items.Remove(StopRemainingRulesKey);
    }
}
