using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted.Controllers;
using Sample.Hosted.Pages.Shared.Models;

namespace Sample.Hosted.Helpers;

public static class Get
{
    public static TempDataKeys TempDataKeys { get; } = new();
    public static ViewDataKeys ViewDataKeys { get; } = new();

    public static RoleFor Role { get; } = new();
    public static ClaimFor Claim { get; } = new();

    public static CspFor Csp { get; } = new();

    public static PageFor.PageFor Page { get; } = PageCollectionBase<PageFor.PageFor>.PageCollection;
    public static SampleEndpointFor Endpoint { get; } = (SampleEndpointFor)EndpointCollectionBase<SampleProgram>.EndpointCollection!;

    public static SubNavigationFor SubNavigation { get; } = new();
    public static LayoutOptionsFor LayoutOptions { get; } = new();
}