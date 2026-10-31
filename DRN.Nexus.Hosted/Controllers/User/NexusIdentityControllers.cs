using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.SharedKernel.Attributes;
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
    [Sensitive(false, Justification = "Email confirmation endpoint descriptor.")]
    public override ApiEndpoint EmailEndpoint => Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
}

[Route(UserApiFor.ControllerRouteTemplate)]
public class NexusIdentityRegister : IdentityRegisterControllerBase<NexusUser>
{
    [Sensitive(false, Justification = "Email confirmation endpoint descriptor.")]
    public override ApiEndpoint EmailEndpoint => Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
}
