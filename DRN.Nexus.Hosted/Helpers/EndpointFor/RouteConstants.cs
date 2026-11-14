using DRN.Framework.Hosting.Nexus;

namespace DRN.Nexus.Hosted.Helpers.EndpointFor;

public partial class SampleApiFor
{
    public const string Prefix = $"/{NexusEndpoints.Prefix}";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";
}

public partial class UserApiFor
{
    public const string Prefix = $"/{NexusEndpoints.User}";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";
}
