using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Helpers.PageFor;

public class TestPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["Test"];

    public string DeveloperView { get; init; } = string.Empty;
    public string Htmx { get; init; } = string.Empty;
}