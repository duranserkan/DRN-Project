using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Helpers.PageFor;

public class RootPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = [];

    public string Root { get; init; } = "/";
    public string Home { get; init; } = string.Empty;
    public string HomeAnonymous { get; init; } = string.Empty;
    public string Swagger { get; init; } = string.Empty;
    public string AccessDenied { get; init; } = string.Empty;
    public string SomethingWentWrong { get; init; } = string.Empty;
    public string NotFound { get; init; } = string.Empty;
    public string BadRequest { get; init; } = string.Empty;
}
