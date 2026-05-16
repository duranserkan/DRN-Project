using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace DRN.Framework.Hosting.RateLimiting;

internal static class RateLimitEndpointMetadata
{
    internal const string GlobalPolicyName = "global";

    internal static bool IsRateLimitingDisabled(HttpContext context)
        => context.GetEndpoint()?.Metadata.GetMetadata<DisableRateLimitingAttribute>() != null;

    internal static string GetPolicyName(HttpContext context)
    {
        var policyName = GetEnabledPolicyName(context);
        return string.IsNullOrWhiteSpace(policyName)
            ? GlobalPolicyName
            : policyName;
    }

    internal static bool IsPolicyMatch(HttpContext context, string? policyName)
    {
        if (policyName == null)
            return true;

        var endpointPolicy = GetPolicyName(context);
        var requestedPolicy = string.IsNullOrWhiteSpace(policyName)
            ? GlobalPolicyName
            : policyName;

        return string.Equals(endpointPolicy, requestedPolicy, StringComparison.Ordinal);
    }

    private static string? GetEnabledPolicyName(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
}
