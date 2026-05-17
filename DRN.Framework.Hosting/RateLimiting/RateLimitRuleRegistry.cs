using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.RateLimiting;

/// <summary>
/// Caches singleton rules and detects whether scoped rule resolution is needed.
/// </summary>
[Singleton<RateLimitRuleRegistry>]
public class RateLimitRuleRegistry
{
    private static readonly object ScopedRulesKey = new();

    public RateLimitRuleRegistry(
        IEnumerable<ISingletonRateLimitRule> singletonRules,
        IServiceScopeFactory scopeFactory)
    {
        SingletonRules = SortRules(singletonRules);
        using var scope = scopeFactory.CreateScope();
        var scopedRules = SortRules(scope.ServiceProvider.GetServices<IScopedRateLimitRule>());
        HasScopedRules = scopedRules.Length > 0;
        ScopedRuleRegistrations = scopedRules
            .Select((rule, index) => new ScopedRateLimitRuleRegistration(index, rule.Order, rule.ShortCircuitOnMatch))
            .ToArray();
    }

    internal ISingletonRateLimitRule[] SingletonRules { get; }

    internal bool HasScopedRules { get; }

    internal ScopedRateLimitRuleRegistration[] ScopedRuleRegistrations { get; }

    internal IScopedRateLimitRule? GetScopedRule(HttpContext context, ScopedRateLimitRuleRegistration registration)
    {
        var rules = GetScopedRules(context);
        return registration.Index < rules.Length ? rules[registration.Index] : null;
    }

    private static IScopedRateLimitRule[] GetScopedRules(HttpContext context)
    {
        if (context.Items.TryGetValue(ScopedRulesKey, out var value) && value is IScopedRateLimitRule[] scopedRules)
            return scopedRules;

        scopedRules = SortRules(context.RequestServices.GetServices<IScopedRateLimitRule>());
        context.Items[ScopedRulesKey] = scopedRules;

        return scopedRules;
    }

    private static T[] SortRules<T>(IEnumerable<T> rules)
        where T : IRateLimitRule
    {
        var index = 0;
        var sortedRules = rules
            .Select(rule =>
            {
                ValidatePolicyName(rule);
                return (Rule: rule, Index: index++);
            })
            .OrderBy(entry => entry.Rule.Order)
            .ThenByDescending(entry => entry.Rule.ShortCircuitOnMatch)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Rule)
            .ToArray();

        return sortedRules;
    }

    private static void ValidatePolicyName(IRateLimitRule rule)
    {
        var policyName = rule.PolicyName;
        if (policyName == null || !string.IsNullOrWhiteSpace(policyName))
            return;

        throw new InvalidOperationException(
            $"{rule.GetType().FullName} rate limit rule {nameof(IRateLimitRule.PolicyName)} must be null or a non-empty value.");
    }

    internal sealed record ScopedRateLimitRuleRegistration(
        int Index,
        int Order,
        bool ShortCircuitOnMatch);
}
