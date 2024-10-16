using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace Sample.Hosted.EndpointRouteBuilderExtensions;

public interface IIdentityEmailConfirmationEndpoint
{
    string? Name { get; internal set; }
}

[Singleton<IIdentityEmailConfirmationEndpoint>]
public class IdentityEmailConfirmationEndpoint : IIdentityEmailConfirmationEndpoint
{
    public string? Name { get; set; }
}