using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted;
using Sample.Hosted.Controllers.Sample;
using Sample.Hosted.Controllers.User;

// Generated roots: {baseNamespace}.EndpointFor.{rootPrefix}EndpointFor and {baseNamespace}.PageFor.{rootPrefix}PageFor.
// {programNamespace}: namespace declaring the type passed to typeof, here Sample.Hosted.
// {baseNamespace}: {programNamespace}.Helpers by default, here Sample.Hosted.Helpers. Set Namespace to replace it.
// For a type with no namespace, {baseNamespace} defaults to Helpers.
// {rootPrefix}: type name with a trailing Program removed, here Sample. Set Name to replace this prefix.
// Program alone uses App; a name without the Program suffix stays unchanged.
// Thus SampleProgram generates SampleEndpointFor/SamplePageFor; Program generates AppEndpointFor/AppPageFor.
// Namespaces follow the program namespace, not this file's folder. No files are written into Helpers.
// Controller namespaces define groups. Razor @page paths define page groups: Pages/Home.cshtml -> Get.Page.Root.Home.
// SampleProgram.ConfigureEndpointCollection returns SampleEndpointFor.EndpointCollection for startup binding.
[assembly: HostedAccessors(typeof(SampleProgram))]

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
