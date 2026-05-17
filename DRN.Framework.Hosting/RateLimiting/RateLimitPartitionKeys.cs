using DRN.Framework.Utils.Auth;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.RateLimiting;

internal static class RateLimitPartitionKeys
{
    internal static string GetPreAuthPartitionKey(HttpContext context) => GetIpPartitionKey(context);

    internal static string GetPostAuthPartitionKey(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(ClaimConventions.NameIdentifier)?.Value
                         ?? context.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
                return GetAuthenticatedPartitionKey("user", context.User.Identity.AuthenticationType, userId);

            // Avoid using Identity.Name as a fallback because it is often mutable and may contain PII.
        }

        return GetIpPartitionKey(context);
    }

    private static string GetAuthenticatedPartitionKey(string kind, string? authenticationType, string value) =>
        string.IsNullOrWhiteSpace(authenticationType)
            ? $"{kind}:{value}"
            : $"{kind}:{authenticationType}:{value}";

    private static string GetIpPartitionKey(HttpContext context) =>
        $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}
