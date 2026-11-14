using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Hosting.Identity.Controllers;
using Sample.Domain.Users;

namespace Sample.Hosted.Controllers.User;

[Route("/Api/User/[controller]")]
public class SampleIdentityLoginController : IdentityLoginControllerBase<SampleUser>;

[Route("/Api/User/[controller]")]
public class SampleIdentityPasswordController : IdentityPasswordControllerBase<SampleUser>;

[Route("/Api/User/[controller]")]
public class SampleIdentityManagementController : IdentityManagementControllerBase<SampleUser>
{
    [Sensitive(false, Justification = "Email confirmation endpoint descriptor.")]
    public override ApiEndpoint EmailEndpoint => Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
}

[Route("/Api/User/[controller]")]
public class SampleIdentityRegister : IdentityRegisterControllerBase<SampleUser>
{
    [Sensitive(false, Justification = "Email confirmation endpoint descriptor.")]
    public override ApiEndpoint EmailEndpoint => Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
}
