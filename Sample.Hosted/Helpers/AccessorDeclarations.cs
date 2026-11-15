using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted;
using Sample.Hosted.Controllers.Sample;
using Sample.Hosted.Controllers.User;

// SampleProgram automatically generates SampleEndpointFor and SamplePageFor under Sample.Hosted.Helpers.
// This file contains only customizations. Generated sources remain compiler-owned under Helpers hint folders.

// Preserve the existing group type UserIdentityFor instead of UserIdentityApiFor.
[assembly: AccessorGroup(typeof(SampleProgram), "User.Identity", "UserIdentityFor")]

// Avoid an ExceptionFor type collision with DRN.Framework.SharedKernel in consumers that import both namespaces.
[assembly: AccessorController(typeof(SampleProgram), typeof(ExceptionController), TypeName = "ExceptionControllerFor")]

// Preserve the identity-group path and UserIdentityRegisterFor type instead of Sample-prefixed defaults.
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityRegister), "User.Identity.RegisterController", TypeName = "UserIdentityRegisterFor")]

// Preserve the identity-group path and UserIdentityLoginFor type instead of Sample-prefixed defaults.
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityLoginController), "User.Identity.LoginController", TypeName = "UserIdentityLoginFor")]

// Preserve User.Identity.PasswordController instead of the conventional User.SampleIdentityPassword path.
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityPasswordController), "User.Identity.PasswordController")]

// Preserve User.Identity.ManagementController instead of the conventional User.SampleIdentityManagement path.
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityManagementController), "User.Identity.ManagementController")]
