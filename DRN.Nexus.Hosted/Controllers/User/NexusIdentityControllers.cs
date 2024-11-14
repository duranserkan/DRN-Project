using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Identity.Controllers;
using DRN.Nexus.Domain.User;

namespace DRN.Nexus.Hosted.Controllers.User;

[Route(UserApiFor.ControllerRouteTemplate)]
public class NexusIdentityLoginController : IdentityLoginControllerBase<NexusUser>;

[Route(UserApiFor.ControllerRouteTemplate)]
public class NexusIdentityPasswordController : IdentityPasswordControllerBase<NexusUser>;

[Route(UserApiFor.ControllerRouteTemplate)]
public class NexusIdentityManagementController : IdentityManagementControllerBase<NexusUser>
{
    public override ApiEndpoint EmailEndpoint => NexusEndpointFor.User.Identity.RegisterController.ConfirmEmail;
}

[Route(UserApiFor.ControllerRouteTemplate)]
public class NexusIdentityRegister : IdentityRegisterControllerBase<NexusUser>
{
    public override ApiEndpoint EmailEndpoint => NexusEndpointFor.User.Identity.RegisterController.ConfirmEmail;
}