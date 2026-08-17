using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted.Controllers.User;
using Sample.Hosted.Controllers.User.Profile;

namespace Sample.Hosted.Helpers.EndpointFor;

public class UserApiFor
{
    public const string Prefix = "/Api/User";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";

    public UserIdentityFor Identity { get; } = new();
    public ProfilePictureFor PP { get; } = new();

}

public class UserIdentityFor
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public UserIdentityRegisterFor RegisterController { get; } = new();
    public UserIdentityLoginFor LoginController { get; } = new();
    public SampleIdentityPasswordFor PasswordController { get; } = new();
    public SampleIdentityManagementFor ManagementController { get; } = new();
}

public class UserIdentityLoginFor() : ControllerForBase<SampleIdentityLoginController>(UserApiFor.ControllerRouteTemplate)
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Login { get; private set; } = null!;
    public ApiEndpoint Refresh { get; private set; } = null!;
}

public class UserIdentityRegisterFor() : ControllerForBase<SampleIdentityRegister>(UserApiFor.ControllerRouteTemplate)
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Register { get; private set; } = null!;

    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint ConfirmEmail { get; private set; } = null!;
    public ApiEndpoint ResendConfirmationEmail { get; private set; } = null!;
}

public class ProfilePictureFor() : ControllerForBase<ProfilePictureController>(UserApiFor.ControllerRouteTemplate)
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Get { get; private set; } = null!;
}

public class SampleIdentityPasswordFor() : ControllerForBase<SampleIdentityPasswordController>(UserApiFor.ControllerRouteTemplate)
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Forgot { get; private set; } = null!;
    public ApiEndpoint Reset { get; private set; } = null!;
}

public class SampleIdentityManagementFor() : ControllerForBase<SampleIdentityManagementController>(UserApiFor.ControllerRouteTemplate)
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint TwoFactorAuth { get; private set; } = null!;
    public ApiEndpoint GetInfo { get; private set; } = null!;
    public ApiEndpoint PostInfo { get; private set; } = null!;
}