using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Helpers.PageFor;

public class SystemPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["System"];

    public string Setup { get; init; } = string.Empty;
}