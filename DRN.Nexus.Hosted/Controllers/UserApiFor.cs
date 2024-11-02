using DRN.Framework.Hosting.Endpoints;
using DRN.Nexus.Hosted.Controllers.User.Identity;

namespace DRN.Nexus.Hosted.Controllers;

public class UserApiFor
{
    public const string Prefix = "/Api/User";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";

    public UserIdentityFor Identity { get; } = new();
}

public class UserIdentityFor() : ControllerForBase<IdentityController>(UserApiFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Register { get; private set; } = null!;
    public ApiEndpoint Login { get; private set; } = null!;
    public ApiEndpoint Refresh { get; private set; } = null!;
    public UserIdentityConfirmationFor Confirmation { get; } = new();
}

public class UserIdentityConfirmationFor()
    : ControllerForBase<IdentityConfirmationController>(UserApiFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint ConfirmEmail { get; private set; } = null!;
}