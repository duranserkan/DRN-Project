using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Endpoints;

public class PageEndpoint
{
    public PageEndpoint(RouteEndpoint[] endpoints)
    {
        Endpoints = endpoints;
        PrimaryEndpoint = endpoints[0];
        PageRoute = PrimaryEndpoint.Metadata.GetMetadata<PageRouteMetadata>()!;
        ActionDescriptor = PrimaryEndpoint.Metadata.GetMetadata<PageActionDescriptor>()!;
        RouteName = PrimaryEndpoint.Metadata.GetMetadata<RouteNameMetadata>()!;
        RouteDiagnostics = PrimaryEndpoint.Metadata.GetMetadata<IRouteDiagnosticsMetadata>()!;
        EndpointByRoutePatterns = endpoints.ToDictionary(e => e.RoutePattern.RawText!, e => e);
        PrimaryRelativePath = PrimaryEndpoint.RoutePattern.RawText!;
    }

    public RouteEndpoint[] Endpoints { get; }
    public RouteEndpoint PrimaryEndpoint { get; }
    public PageRouteMetadata PageRoute { get; }
    public PageActionDescriptor ActionDescriptor { get; }
    public RouteNameMetadata RouteName { get; }
    public IRouteDiagnosticsMetadata RouteDiagnostics { get; }
    public IReadOnlyDictionary<string, RouteEndpoint> EndpointByRoutePatterns { get; }
    public string PrimaryRelativePath { get; }
}