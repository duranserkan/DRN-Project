using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.RateLimiting;

internal readonly record struct RateLimitRuleMatch(IRateLimitRule Rule, RateLimitRuleResult Result);

internal static class RateLimitRuleHttpContextItems
{
    private const string RuleSelectionKey = "__DRN_RateLimitRuleSelection";

    internal static void SetRateLimitRuleMatch(this HttpContext context, IRateLimitRule rule, RateLimitRuleResult result) =>
        GetOrCreateSelection(context).SetMatch(rule, result);

    internal static RateLimitRuleMatch? GetRateLimitRuleMatch(this HttpContext context) =>
        GetSelection(context)?.LastMatch;

    internal static void SetRejectedRateLimitRuleMatch(this HttpContext context)
    {
        var selection = GetSelection(context);
        if (selection?.LastMatch is { } match)
            selection.RejectedMatch = match;
    }

    internal static RateLimitRuleMatch? GetRejectedRateLimitRuleMatch(this HttpContext context) =>
        GetSelection(context)?.RejectedMatch;

    internal static void StopRemainingRateLimitRules(this HttpContext context) =>
        GetOrCreateSelection(context).StopRemainingRules = true;

    internal static bool ShouldStopRemainingRateLimitRules(this HttpContext context) =>
        GetSelection(context)?.StopRemainingRules == true;

    internal static void ClearRateLimitRuleSelection(this HttpContext context) =>
        context.Items.Remove(RuleSelectionKey);

    private static RateLimitRuleSelection? GetSelection(HttpContext context) =>
        context.Items.TryGetValue(RuleSelectionKey, out var value) ? value as RateLimitRuleSelection : null;

    private static RateLimitRuleSelection GetOrCreateSelection(HttpContext context)
    {
        var selection = GetSelection(context);
        if (selection != null)
            return selection;

        selection = new RateLimitRuleSelection();
        context.Items[RuleSelectionKey] = selection;

        return selection;
    }

    private sealed class RateLimitRuleSelection
    {
        internal RateLimitRuleMatch? LastMatch { get; private set; }
        internal RateLimitRuleMatch? RejectedMatch { get; set; }
        internal bool StopRemainingRules { get; set; }

        internal void SetMatch(IRateLimitRule rule, RateLimitRuleResult result) =>
            LastMatch = new RateLimitRuleMatch(rule, result);
    }
}
