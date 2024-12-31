using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Endpoints;

public class PageEndpoint(RouteEndpoint endpoint)
{
    public RouteEndpoint Endpoint { get; } = endpoint;
    public PageRouteMetadata PageRoute { get; } = endpoint.Metadata.GetMetadata<PageRouteMetadata>()!;
    public PageActionDescriptor ActionDescriptor { get; } = endpoint.Metadata.GetMetadata<PageActionDescriptor>()!;
    public RouteNameMetadata RouteName { get; } = endpoint.Metadata.GetMetadata<RouteNameMetadata>()!;
    public IRouteDiagnosticsMetadata RouteDiagnostics { get; } = endpoint.Metadata.GetMetadata<IRouteDiagnosticsMetadata>()!;
}