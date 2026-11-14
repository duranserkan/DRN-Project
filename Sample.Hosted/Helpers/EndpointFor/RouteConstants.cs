namespace Sample.Hosted.Helpers.EndpointFor;

public partial class QaApiFor
{
    public const string Prefix = "/Api/QA";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";
}

public partial class SampleApiFor
{
    public const string Prefix = "/Api/Sample";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";
}

public partial class UserApiFor
{
    public const string Prefix = "/Api/User";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";
}
