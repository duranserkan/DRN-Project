using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Helpers.PageFor;

public class SamplePageFor : PageCollectionBase<SamplePageFor>
{
    public RootPageFor Root { get; } = new();
    public TestPageFor Test { get; } = new();
    public UserPageFor User { get; } = new();
    public SystemPageFor System { get; } = new();
}