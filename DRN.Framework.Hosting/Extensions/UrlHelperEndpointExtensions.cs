using System;
using System.Linq;
using DRN.Framework.Hosting.Endpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

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
        ArgumentNullException.ThrowIfNull(endpoint);

        var actions = endpoint.ActionDescriptor;
        if (actions is not { Length: > 0 })
            throw new InvalidOperationException($"Endpoint '{endpoint.EndpointName}' has no initialized MVC action metadata.");

        var action = actions[0];
        var area = GetArea(action);
        if (actions.Any(candidate => candidate.ActionName != action.ActionName ||
                                     candidate.ControllerName != action.ControllerName ||
                                     GetArea(candidate) != area))
            throw new InvalidOperationException($"Endpoint '{endpoint.EndpointName}' identifies multiple MVC actions.");

        // Bind the destination's area explicitly so the current page's area cannot leak into this URL.
        var routeValues = new RouteValueDictionary(values) { ["area"] = area };
        return urlHelper.Action(action.ActionName, action.ControllerName, routeValues)
            ?? throw new InvalidOperationException($"Could not generate a URL for endpoint '{endpoint.EndpointName}'.");
    }

    private static string GetArea(ControllerActionDescriptor action) =>
        action.RouteValues.TryGetValue("area", out var area) ? area ?? string.Empty : string.Empty;
}
