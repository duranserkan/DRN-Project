using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted.Helpers.PageFor;
using Sample.Hosted.Pages.Shared.Models;

namespace Sample.Hosted.Helpers;


public static class Get
{
    //PageFor and EndpointFor must be initialized first since they might be referenced from other helpers such as SubNavigationFor
    public static SamplePageFor Page { get; } = PageCollectionBase<SamplePageFor>.PageCollection;
    public static SampleEndpointFor Endpoint { get; } = (SampleEndpointFor)EndpointCollectionBase<SampleProgram>.EndpointCollection!;
    
    public static CspFor Csp { get; } = new();
    public static RoleFor Role { get; } = new();
    public static ClaimFor Claim { get; } = new();

    public static TempDataKeys TempDataKeys { get; } = new();
    public static ViewDataKeys ViewDataKeys { get; } = new();
    
    public static SubNavigationFor SubNavigation { get; } = new();
    public static LayoutOptionsFor LayoutOptions { get; } = new();
}