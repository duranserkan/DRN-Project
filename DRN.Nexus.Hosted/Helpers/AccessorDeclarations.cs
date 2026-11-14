using DRN.Framework.Hosting.Endpoints;
using DRN.Nexus.Hosted;
using DRN.Nexus.Hosted.Controllers.User;

[assembly: HostedAccessors(typeof(NexusProgram))]
// Preserve the existing group type UserIdentityFor instead of UserIdentityApiFor.
[assembly: AccessorGroup(typeof(NexusProgram), "User.Identity", "UserIdentityFor")]
// Preserve the identity-group path and UserIdentityRegisterFor type instead of Nexus-prefixed defaults.
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityRegister), "User.Identity.RegisterController", TypeName = "UserIdentityRegisterFor")]
// Preserve the identity-group path and UserIdentityLoginFor type instead of Nexus-prefixed defaults.
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityLoginController), "User.Identity.LoginController", TypeName = "UserIdentityLoginFor")]
// Preserve the identity-group path and UserIdentityPasswordFor type instead of Nexus-prefixed defaults.
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityPasswordController), "User.Identity.PasswordController", TypeName = "UserIdentityPasswordFor")]
// Preserve the identity-group path and UserIdentityManagementFor type instead of Nexus-prefixed defaults.
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityManagementController), "User.Identity.ManagementController", TypeName = "UserIdentityManagementFor")]
