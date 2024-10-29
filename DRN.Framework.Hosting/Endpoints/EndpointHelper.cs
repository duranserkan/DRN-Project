using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Endpoints;

public interface IEndpointHelper
{
    EndpointDataSource EndpointDataSource { get; }
    LinkGenerator LinkGenerator { get; }
    RouteEndpoint? GetEndpoint(string controllerName, string actionName);
}

[Singleton<IEndpointHelper>]
public class EndpointHelper(EndpointDataSource endpointDataSource, LinkGenerator linkGenerator) : IEndpointHelper
{
    public EndpointDataSource EndpointDataSource { get; } = endpointDataSource;
    public LinkGenerator LinkGenerator { get; } = linkGenerator;

    public RouteEndpoint? GetEndpoint(string controllerName, string actionName)
    {
        var endpoint = EndpointDataSource.Endpoints
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