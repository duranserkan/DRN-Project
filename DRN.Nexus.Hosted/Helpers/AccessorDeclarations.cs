using DRN.Framework.Hosting.Endpoints;
using DRN.Nexus.Hosted;
using DRN.Nexus.Hosted.Controllers.User;

[assembly: HostedAccessors(typeof(NexusProgram))]
[assembly: AccessorGroup(typeof(NexusProgram), "User.Identity", "UserIdentityFor")]
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityRegister), "User.Identity.RegisterController", TypeName = "UserIdentityRegisterFor")]
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityLoginController), "User.Identity.LoginController", TypeName = "UserIdentityLoginFor")]
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityPasswordController), "User.Identity.PasswordController", TypeName = "UserIdentityPasswordFor")]
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityManagementController), "User.Identity.ManagementController", TypeName = "UserIdentityManagementFor")]
