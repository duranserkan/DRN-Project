using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted;
using Sample.Hosted.Controllers.QA;
using Sample.Hosted.Controllers.Sample;
using Sample.Hosted.Controllers.User;
using Sample.Hosted.Controllers.User.Profile;
using Sample.Hosted.Helpers.EndpointFor;

[assembly: HostedAccessors(typeof(SampleProgram), Pages = true)]
[assembly: AccessorGroup(typeof(SampleProgram), "QA", "QaApiFor")]
[assembly: AccessorGroup(typeof(SampleProgram), "User.Identity", "UserIdentityFor")]
[assembly: AccessorController(typeof(SampleProgram), typeof(TagController), "QA.Tag", RouteTemplate = QaApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(SampleProgram), typeof(CategoryController), "QA.Category", RouteTemplate = QaApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(SampleProgram), typeof(PrivateController), "Sample.Private", RouteTemplate = SampleApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(SampleProgram), typeof(WeatherForecastController), "Sample.WeatherForecast", RouteTemplate = SampleApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(SampleProgram), typeof(ExceptionController), "Sample.Exception", TypeName = "ExceptionControllerFor", RouteTemplate = SampleApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityRegister), "User.Identity.RegisterController", TypeName = "UserIdentityRegisterFor", RouteTemplate = UserApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityLoginController), "User.Identity.LoginController", TypeName = "UserIdentityLoginFor", RouteTemplate = UserApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityPasswordController), "User.Identity.PasswordController", TypeName = "SampleIdentityPasswordFor", RouteTemplate = UserApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(SampleProgram), typeof(SampleIdentityManagementController), "User.Identity.ManagementController", TypeName = "SampleIdentityManagementFor", RouteTemplate = UserApiFor.ControllerRouteTemplate)]
[assembly: AccessorController(typeof(SampleProgram), typeof(ProfilePictureController), "User.PP", TypeName = "ProfilePictureFor", RouteTemplate = UserApiFor.ControllerRouteTemplate)]
