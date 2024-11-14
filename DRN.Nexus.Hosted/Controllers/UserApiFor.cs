using DRN.Framework.Hosting.Endpoints;
using DRN.Nexus.Hosted.Controllers.User;

namespace DRN.Nexus.Hosted.Controllers;

public class UserApiFor
{
    public const string Prefix = "/Api/User";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";

    public UserIdentityFor Identity { get; } = new();
}

public class UserIdentityFor
{
    //By convention Endpoint name should match Action name and property should have setter;
    public UserIdentityRegisterFor RegisterController { get; } = new();
    public UserIdentityLoginFor LoginController { get; } = new();
}

public class UserIdentityLoginFor() : ControllerForBase<NexusIdentityLoginController>(UserApiFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Login { get; private set; } = null!;
}

public class UserIdentityRegisterFor() : ControllerForBase<NexusIdentityRegister>(UserApiFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Register { get; private set; } = null!;

    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint ConfirmEmail { get; private set; } = null!;
}