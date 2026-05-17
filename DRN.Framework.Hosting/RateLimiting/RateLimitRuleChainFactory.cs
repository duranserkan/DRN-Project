using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.RateLimiting;

internal static class RateLimitRuleChainFactory
{
    internal static PartitionedRateLimiter<HttpContext> Create(
        RateLimitRuleRegistry registry,
        RateLimitRulePhase phase,
        Func<IRateLimitRule, HttpContext, RateLimitRuleResult?> evaluate,
        bool includeScopedRules = true)
    {
        var index = 0;
        var limiters = new List<RateLimiterEntry>
        {
            // Reset must stay first even if a custom rule uses int.MinValue with ShortCircuitOnMatch.
            new(int.MinValue, true, index++, CreateSelectionResetLimiter(phase))
        };

        foreach (var rule in registry.SingletonRules)
            limiters.Add(new RateLimiterEntry(rule.Order, rule.ShortCircuitOnMatch, index++,
                CreateSingletonRuleLimiter(rule, phase, evaluate)));

        if (includeScopedRules && registry.HasScopedRules)
            foreach (var registration in registry.ScopedRuleRegistrations)
                limiters.Add(new RateLimiterEntry(registration.Order, registration.ShortCircuitOnMatch, index++,
                    CreateScopedRuleLimiter(registration, phase, evaluate)));

        var orderedLimiters = limiters
            .OrderBy(entry => entry.Order)
            .ThenByDescending(entry => entry.ShortCircuitOnMatch)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Limiter)
            .ToArray();

        return orderedLimiters.Length == 1
            ? orderedLimiters[0]
            : PartitionedRateLimiter.CreateChained(orderedLimiters);
    }

    private static PartitionedRateLimiter<HttpContext> CreateSelectionResetLimiter(RateLimitRulePhase phase)
    {
        var partitionKey = $"{phase}:selection-reset";
        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            context.ClearRateLimitRuleSelection();
            return RateLimitPartition.GetNoLimiter(partitionKey);
        });
    }

    private static PartitionedRateLimiter<HttpContext> CreateSingletonRuleLimiter(
        IRateLimitRule rule,
        RateLimitRulePhase phase,
        Func<IRateLimitRule, HttpContext, RateLimitRuleResult?> evaluate) =>
        new RateLimitRuleRejectionTrackingLimiter(PartitionedRateLimiter.Create<HttpContext, RateLimitRulePartitionKey>(context =>
            TrySelectRulePartition(context, rule, phase, evaluate) ?? GetNoLimiter(phase, rule, RateLimitRulePartitionKeyKind.Skip)));

    private static PartitionedRateLimiter<HttpContext> CreateScopedRuleLimiter(
        RateLimitRuleRegistry.ScopedRateLimitRuleRegistration registration,
        RateLimitRulePhase phase,
        Func<IRateLimitRule, HttpContext, RateLimitRuleResult?> evaluate) =>
        new RateLimitRuleRejectionTrackingLimiter(PartitionedRateLimiter.Create<HttpContext, RateLimitRulePartitionKey>(context =>
        {
            if (context.ShouldStopRemainingRateLimitRules())
                return RateLimitPartition.GetNoLimiter(RateLimitRulePartitionKey.NoLimiter(
                    phase,
                    RateLimitRulePartitionKeyKind.ScopedStopped,
                    registration.Index));

            var rule = RateLimitRuleRegistry.GetScopedRule(context, registration);
            if (rule == null)
                return RateLimitPartition.GetNoLimiter(RateLimitRulePartitionKey.NoLimiter(
                    phase,
                    RateLimitRulePartitionKeyKind.ScopedMissing,
                    registration.Index));

            return TrySelectRulePartition(context, rule, phase, evaluate) ?? GetNoLimiter(phase, rule, RateLimitRulePartitionKeyKind.Skip);
        }));

    private static RateLimitPartition<RateLimitRulePartitionKey>? TrySelectRulePartition(
        HttpContext context,
        IRateLimitRule rule,
        RateLimitRulePhase phase,
        Func<IRateLimitRule, HttpContext, RateLimitRuleResult?> evaluate)
    {
        if (context.ShouldStopRemainingRateLimitRules())
            return GetNoLimiter(phase, rule, RateLimitRulePartitionKeyKind.Stopped);

        if (!RateLimitEndpointMetadata.IsPolicyMatch(context, rule.PolicyName))
            return null;

        var result = evaluate(rule, context);
        if (!result.HasValue)
            return null;

        var ruleResult = result.GetValueOrDefault();
        context.SetRateLimitRuleMatch(rule, ruleResult);
        if (rule.ShortCircuitOnMatch || ruleResult.StopRemainingRules)
            context.StopRemainingRateLimitRules();

        return ruleResult.Action == RateLimitRuleAction.Allow
            ? GetNoLimiter(phase, rule, RateLimitRulePartitionKeyKind.Allow)
            : CreateRulePartition(rule, phase, ruleResult);
    }

    private static RateLimitPartition<RateLimitRulePartitionKey> CreateRulePartition(IRateLimitRule rule, RateLimitRulePhase phase, RateLimitRuleResult result) =>
        RateLimitPartition.Get(
            RateLimitRulePartitionKey.Limit(phase, rule.GetType(), result),
            static key => key.CreateLimiter());

    private static RateLimitPartition<RateLimitRulePartitionKey> GetNoLimiter(
        RateLimitRulePhase phase,
        IRateLimitRule rule,
        RateLimitRulePartitionKeyKind reason) =>
        RateLimitPartition.GetNoLimiter(RateLimitRulePartitionKey.NoLimiter(phase, rule.GetType(), reason));

    private readonly record struct RateLimiterEntry(
        int Order,
        bool ShortCircuitOnMatch,
        int Index,
        PartitionedRateLimiter<HttpContext> Limiter);
}
