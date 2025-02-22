using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Identity.Controllers;
using Sample.Domain.Users;
using Sample.Hosted.Helpers;

namespace Sample.Hosted.Controllers.User;

[Route(UserApiFor.ControllerRouteTemplate)]
public class SampleIdentityLoginController : IdentityLoginControllerBase<SampleUser>;

[Route(UserApiFor.ControllerRouteTemplate)]
public class SampleIdentityPasswordController : IdentityPasswordControllerBase<SampleUser>;

[Route(UserApiFor.ControllerRouteTemplate)]
public class SampleIdentityManagementController : IdentityManagementControllerBase<SampleUser>
{
    public override ApiEndpoint EmailEndpoint => Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
}

[Route(UserApiFor.ControllerRouteTemplate)]
public class SampleIdentityRegister : IdentityRegisterControllerBase<SampleUser>
{
    public override ApiEndpoint EmailEndpoint => Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
}