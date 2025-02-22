using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted.Pages;

namespace Sample.Hosted.Utils;

public static class Get
{
    public static RoleFor Role { get; } = new();
    public static ClaimFor Claim { get; } = new();
    public static CspFor Csp { get; } = new();
    public static PageFor Page { get; } = PageCollectionBase<PageFor>.PageCollection;
}