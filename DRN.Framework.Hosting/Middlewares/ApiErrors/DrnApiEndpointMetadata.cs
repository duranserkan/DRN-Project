using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace DRN.Framework.Hosting.Middlewares.ApiErrors;

internal static class DrnApiEndpointMetadata
{
    internal static bool IsApi(IEnumerable<object> metadata) =>
        metadata.Any(item => item is IApiBehaviorMetadata or IDisableCookieRedirectMetadata);

    internal static bool IsDisabled(IEnumerable<object> metadata) =>
        metadata.Any(item => item is DisableDrnProblemDetailsAttribute);
}
