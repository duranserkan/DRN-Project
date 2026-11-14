using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Endpoints;

internal static class EndpointActionMetadata
{
    internal static (ControllerActionDescriptor Action, RouteValueDictionary Values) Resolve(ApiEndpoint endpoint, object? values)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var actions = endpoint.ActionDescriptor;
        if (actions is not { Length: > 0 })
            throw new InvalidOperationException($"Endpoint '{endpoint.EndpointName}' has no initialized MVC action metadata.");
        if (endpoint.HasAmbiguousActionAddress)
            throw new InvalidOperationException($"Endpoint '{endpoint.EndpointName}' shares its MVC address with another action. Use distinct controller/action names or areas, or explicitly named routing.");
        var action = actions[0];
        var area = Area(action);
        if (actions.Any(candidate => candidate.ActionName != action.ActionName ||
                                     candidate.ControllerName != action.ControllerName || Area(candidate) != area))
            throw new InvalidOperationException($"Endpoint '{endpoint.EndpointName}' identifies multiple MVC actions.");
        // Copy caller values and bind the destination area, including the empty non-area destination.
        return (action, new RouteValueDictionary(values) { ["area"] = area });
    }

    internal static bool HasAmbiguousAddress(ControllerActionDescriptor[] actions, IEnumerable<RouteEndpoint> routes) => routes
        .Where(route => route.Metadata.GetMetadata<ISuppressLinkGenerationMetadata>()?.SuppressLinkGeneration != true)
        .Select(route => route.Metadata.GetMetadata<ControllerActionDescriptor>())
        .Any(candidate => candidate != null && actions.Any(action =>
            string.Equals(candidate.ActionName, action.ActionName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.ControllerName, action.ControllerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Area(candidate), Area(action), StringComparison.OrdinalIgnoreCase) &&
            (candidate.ControllerTypeInfo != action.ControllerTypeInfo || !Equals(candidate.MethodInfo, action.MethodInfo))));

    private static string Area(ControllerActionDescriptor action) =>
        action.RouteValues.TryGetValue("area", out var area) ? area ?? string.Empty : string.Empty;
}
