namespace Sample.Hosted.Helpers.PageFor;

// This partial extends the generated RootPageFor with URL shortcuts.
// "/" is HomeAnonymous's custom route, and Swagger is not a Razor page.
public partial class RootPageFor
{
    public string Root { get; init; } = "/";
    public string Swagger { get; init; } = "/Swagger";
}
