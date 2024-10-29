using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted.Controllers.User.Identity;
using Sample.Hosted.Controllers.User.Profile;

namespace Sample.Hosted.Controllers;


public class UserApiFor
{
    public const string Prefix = "/Api/User";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";

    public ProfilePictureFor PP { get; } = new();
    public UserIdentityFor Identity { get; } = new();
}

public class UserIdentityFor() : ControllerForBase<IdentityController>(UserApiFor.ControllerRouteTemplate)
{
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

public class ProfilePictureFor()
    : ControllerForBase<ProfilePictureController>(UserApiFor.ControllerRouteTemplate)
{
    //Todo fix profile picture tag helper
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Get { get; private set; } = null!;
}