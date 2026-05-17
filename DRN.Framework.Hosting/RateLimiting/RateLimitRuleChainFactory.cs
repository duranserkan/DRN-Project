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
                    CreateScopedRuleLimiter(registry, registration, phase, evaluate)));

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

    private static PartitionedRateLimiter<HttpContext> CreateSelectionResetLimiter(RateLimitRulePhase phase) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            context.ClearRateLimitRuleSelection();
            return RateLimitPartition.GetNoLimiter($"{phase}:selection-reset");
        });

    private static PartitionedRateLimiter<HttpContext> CreateSingletonRuleLimiter(
        IRateLimitRule rule,
        RateLimitRulePhase phase,
        Func<IRateLimitRule, HttpContext, RateLimitRuleResult?> evaluate) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            TrySelectRulePartition(context, rule, phase, evaluate) ?? GetNoLimiter(phase, rule, "skip"));

    private static PartitionedRateLimiter<HttpContext> CreateScopedRuleLimiter(
        RateLimitRuleRegistry registry,
        RateLimitRuleRegistry.ScopedRateLimitRuleRegistration registration,
        RateLimitRulePhase phase,
        Func<IRateLimitRule, HttpContext, RateLimitRuleResult?> evaluate) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (context.ShouldStopRemainingRateLimitRules())
                return RateLimitPartition.GetNoLimiter($"{phase}:scoped-stopped:{registration.Index}");

            var rule = registry.GetScopedRule(context, registration);
            if (rule == null)
                return RateLimitPartition.GetNoLimiter($"{phase}:scoped-missing:{registration.Index}");

            return TrySelectRulePartition(context, rule, phase, evaluate) ?? GetNoLimiter(phase, rule, "skip");
        });

    private static RateLimitPartition<string>? TrySelectRulePartition(
        HttpContext context,
        IRateLimitRule rule,
        RateLimitRulePhase phase,
        Func<IRateLimitRule, HttpContext, RateLimitRuleResult?> evaluate)
    {
        if (context.ShouldStopRemainingRateLimitRules())
            return GetNoLimiter(phase, rule, "stopped");

        if (!RateLimitEndpointMetadata.IsPolicyMatch(context, rule.PolicyName))
            return null;

        var result = evaluate(rule, context);
        if (result == null)
            return null;

        context.SetRateLimitRuleMatch(rule, result);
        if (rule.ShortCircuitOnMatch || result.StopRemainingRules)
            context.StopRemainingRateLimitRules();

        return result.Action == RateLimitRuleAction.Allow
            ? GetNoLimiter(phase, rule, "allow")
            : CreateRulePartition(rule, phase, result);
    }

    private static RateLimitPartition<string> CreateRulePartition(IRateLimitRule rule, RateLimitRulePhase phase, RateLimitRuleResult result)
    {
        var partitionKey = $"{phase}:{rule.GetType().FullName}:{result.PartitionKey}";
        // DRN re-keys partitions with phase/rule namespacing; .NET caches the limiter by this framework key.
        return new RateLimitPartition<string>(partitionKey, _ => result.Partition.Factory(result.Partition.PartitionKey));
    }

    private static RateLimitPartition<string> GetNoLimiter(RateLimitRulePhase phase, IRateLimitRule rule, string reason) =>
        RateLimitPartition.GetNoLimiter($"{phase}:{reason}:{rule.GetType().FullName}");

    private readonly record struct RateLimiterEntry(
        int Order,
        bool ShortCircuitOnMatch,
        int Index,
        PartitionedRateLimiter<HttpContext> Limiter);
}
