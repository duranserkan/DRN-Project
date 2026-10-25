using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Sample.Domain.Identity;

namespace Sample.Hosted;

//todo: use TimeProvider
//todo: robots.txt example usage
//todo: add razor page login and logout UI integration tests as example
//https://learn.microsoft.com/en-us/aspnet/core/security/
public class SampleProgram : DrnProgramBase<SampleProgram>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
        => Task.FromResult(builder.Services.AddSampleHostedServices(appSettings));

    protected override void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        base.ConfigureSwaggerOptions(options, appSettings);
        options.AddSwagger = true;
        options.AuthorizationPolicy = UserClaims.SwaggerAccess;
    }

    protected override MfaRedirectionConfig ConfigureMFARedirection()
        => new(Get.Page.User.Management.EnableAuthenticator,
            Get.Page.User.LoginWith2Fa,
            Get.Page.User.Login,
            Get.Page.User.Logout, Get.Page.All);

}
