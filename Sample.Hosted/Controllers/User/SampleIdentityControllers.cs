using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Hosting.Identity.Controllers;
using SampleUser = Sample.Domain.SampleIdentity.SampleUser;

namespace Sample.Hosted.Controllers.User;

[Route(ControllerRoutes.User)]
public class SampleIdentityLoginController : IdentityLoginControllerBase<SampleUser>;

[Route(ControllerRoutes.User)]
public class SampleIdentityPasswordController : IdentityPasswordControllerBase<SampleUser>;

[Route(ControllerRoutes.User)]
public class SampleIdentityManagementController : IdentityManagementControllerBase<SampleUser>
{
    [Sensitive(false, Justification = "Email confirmation endpoint descriptor.")]
    public override ApiEndpoint EmailEndpoint => Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
}

[Route(ControllerRoutes.User)]
public class SampleIdentityRegisterController : IdentityRegisterControllerBase<SampleUser>
{
    [Sensitive(false, Justification = "Email confirmation endpoint descriptor.")]
    public override ApiEndpoint EmailEndpoint => Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
}
