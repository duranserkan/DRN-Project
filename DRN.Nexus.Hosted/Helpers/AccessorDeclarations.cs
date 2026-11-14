using DRN.Framework.Hosting.Endpoints;
using DRN.Nexus.Hosted;
using DRN.Nexus.Hosted.Controllers.Sample;
using DRN.Nexus.Hosted.Controllers.User;
using DRN.Nexus.Hosted.Helpers.EndpointFor;

[assembly: HostedAccessors(typeof(NexusProgram))]
[assembly: AccessorGroup(typeof(NexusProgram), "User.Identity", "UserIdentityFor")]
[assembly: AccessorController(typeof(NexusProgram), typeof(StatusController), "Sample.Status", RouteTemplate = SampleApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(NexusProgram), typeof(WeatherForecastController), "Sample.WeatherForecast", RouteTemplate = SampleApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityRegister), "User.Identity.RegisterController", TypeName = "UserIdentityRegisterFor", RouteTemplate = UserApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityLoginController), "User.Identity.LoginController", TypeName = "UserIdentityLoginFor", RouteTemplate = UserApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityPasswordController), "User.Identity.PasswordController", TypeName = "UserIdentityPasswordFor", RouteTemplate = UserApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(NexusProgram), typeof(NexusIdentityManagementController), "User.Identity.ManagementController", TypeName = "UserIdentityManagementFor", RouteTemplate = UserApiFor.ControllerRouteTemplate)]
