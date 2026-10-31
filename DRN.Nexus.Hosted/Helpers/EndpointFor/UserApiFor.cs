using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Hosting.Nexus;
using DRN.Nexus.Hosted.Controllers.User;

namespace DRN.Nexus.Hosted.Helpers.EndpointFor;

public class UserApiFor
{
    public const string Prefix = $"/{NexusEndpoints.User}";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";

    public UserIdentityFor Identity { get; } = new();
}

public class UserIdentityFor
{
    //By convention Endpoint name should match Action name and property should have setter;
    public UserIdentityRegisterFor RegisterController { get; } = new();
    public UserIdentityLoginFor LoginController { get; } = new();
    [Sensitive(false, Justification = "Endpoint descriptor.")]
    public UserIdentityPasswordFor PasswordController { get; } = new();
    public UserIdentityManagementFor ManagementController { get; } = new();
}

public class UserIdentityLoginFor() : ControllerForBase<NexusIdentityLoginController>(UserApiFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Login { get; private set; } = null!;
    public ApiEndpoint Refresh { get; private set; } = null!;
}

public class UserIdentityRegisterFor() : ControllerForBase<NexusIdentityRegister>(UserApiFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Register { get; private set; } = null!;

    //By convention Endpoint name should match Action name and property should have setter;
    [Sensitive(false, Justification = "Endpoint descriptor.")]
    public ApiEndpoint ConfirmEmail { get; private set; } = null!;
    [Sensitive(false, Justification = "Endpoint descriptor.")]
    public ApiEndpoint ResendConfirmationEmail { get; private set; } = null!;
}

public class UserIdentityPasswordFor() : ControllerForBase<NexusIdentityPasswordController>(UserApiFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Forgot { get; private set; } = null!;
    public ApiEndpoint Reset { get; private set; } = null!;
}

public class UserIdentityManagementFor() : ControllerForBase<NexusIdentityManagementController>(UserApiFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    [Sensitive(false, Justification = "Endpoint descriptor.")]
    public ApiEndpoint TwoFactorAuth { get; private set; } = null!;
    public ApiEndpoint GetInfo { get; private set; } = null!;
    public ApiEndpoint PostInfo { get; private set; } = null!;
}
