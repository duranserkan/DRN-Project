using DRN.Framework.Hosting.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Extensions;

public static class EndpointExtensions
{
    public static string GetEndpointKey(this ControllerActionDescriptor descriptor) 
        => $"{descriptor.ControllerName}.{descriptor.MethodInfo.Name}";
    public static string GetEndpointKey(this ApiEndpoint endpoint) 
        => $"{endpoint.ControllerName}.{endpoint.ActionMethodName}";
    
    public static RouteEndpoint? GetEndpoint(this IReadOnlyDictionary<string, Endpoint> endpoints, string controllerName, string actionName)
    {
        var endpoint = endpoints.Values
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