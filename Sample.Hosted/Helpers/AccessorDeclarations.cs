using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted;
using Sample.Hosted.Controllers.Sample;
using Sample.Hosted.Controllers.User;
using Sample.Hosted.Controllers.User.Profile;

[assembly: HostedAccessors(typeof(SampleProgram), Pages = true)]
[assembly: AccessorGroup(typeof(SampleProgram), "QA", "QaApiFor")]
[assembly: AccessorGroup(typeof(SampleProgram), "User.Identity", "UserIdentityFor")]
[assembly: AccessorController(typeof(SampleProgram), typeof(ExceptionController), "Sample.Exception", TypeName = "ExceptionControllerFor")]
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityRegister), "User.Identity.RegisterController", TypeName = "UserIdentityRegisterFor")]
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityLoginController), "User.Identity.LoginController", TypeName = "UserIdentityLoginFor")]
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityPasswordController), "User.Identity.PasswordController")]
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityManagementController), "User.Identity.ManagementController")]
[assembly: AccessorController(typeof(SampleProgram), typeof(ProfilePictureController), "User.PP")]

// Preserve the empty legacy metadata of accessors introduced without a route override.
[assembly: AccessorController(typeof(SampleProgram), typeof(ClientErrorController), "Sample.ClientError", RouteTemplate = "")]
[assembly: AccessorController(typeof(SampleProgram), typeof(NexusStatusController), "Sample.NexusStatus", RouteTemplate = "")]
