using DRN.Framework.Hosting.Endpoints;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.Extensions;

public static class UrlHelperEndpointExtensions
{
    /// <summary>
    /// Generates a request-aware URL using the endpoint's initialized MVC action metadata.
    /// Route values supply parameters and query values. The target action owns the area.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The endpoint is uninitialized, identifies multiple MVC actions, or cannot generate a URL.
    /// </exception>
    public static string Endpoint(this IUrlHelper urlHelper, ApiEndpoint endpoint, object? values = null)
    {
        ArgumentNullException.ThrowIfNull(urlHelper);
        var (action, routeValues) = EndpointActionMetadata.Resolve(endpoint, values);
        return urlHelper.Action(action.ActionName, action.ControllerName, routeValues)
            ?? throw new InvalidOperationException($"Could not generate a URL for endpoint '{endpoint.EndpointName}'.");
    }
}
