using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Helpers.PageFor;

public class PageFor : PageCollectionBase<PageFor>
{
    public RootPageFor Root { get; } = new();
    public TestPageFor Test { get; } = new();
    public UserPageFor User { get; } = new();
    public SystemPageFor System { get; } = new();
}