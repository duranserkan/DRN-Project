using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Extensions;

public static class EndpointExtensions
{
    public static RouteEndpoint? GetEndpoint(this IReadOnlyList<Endpoint> endpoints, string controllerName, string actionName)
    {
        var endpoint = endpoints
            .OfType<RouteEndpoint>()
            .FirstOrDefault(e =>
            {
                var actionDescriptor = e.Metadata.OfType<ControllerActionDescriptor>().FirstOrDefault();
                return actionDescriptor != null &&
                       actionDescriptor.ControllerTypeInfo.Name.Equals(controllerName, StringComparison.OrdinalIgnoreCase) &&
                       actionDescriptor.ActionName.Equals(actionName, StringComparison.OrdinalIgnoreCase);
            });

        return endpoint;
    }
}