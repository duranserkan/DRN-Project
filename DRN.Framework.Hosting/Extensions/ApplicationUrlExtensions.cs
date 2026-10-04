using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Extensions;

public static class ApplicationUrlExtensions
{
    /// <summary>
    /// Resolves an application-owned /path or ~/path against the processed request PathBase.
    /// Pass route paths, not already-resolved browser URLs. External and relative URLs are unchanged.
    /// </summary>
    public static string ApplicationUrl(this HttpRequest request, string path)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal))
            path = path[1..];

        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) || path.StartsWith("/\\", StringComparison.Ordinal))
            return path;

        return request.PathBase.ToUriComponent().TrimEnd('/') + path;
    }
}
