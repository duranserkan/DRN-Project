using DRN.Framework.Hosting.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Extensions;

public static class LinkGeneratorEndpointExtensions
{
    /// <summary>Generates an absolute URI using effective MVC metadata and the processed request's origin and PathBase.</summary>
    public static string GetUriByEndpoint(this LinkGenerator generator, HttpContext context, ApiEndpoint endpoint, object? values = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(context);
        var (action, routeValues) = EndpointActionMetadata.Resolve(endpoint, values);
        return generator.GetUriByAction(context, action.ActionName, action.ControllerName, routeValues)
            ?? throw new InvalidOperationException($"Could not generate a URI for endpoint '{endpoint.EndpointName}'.");
    }
}
